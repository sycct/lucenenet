﻿using Lucene.Net.Analysis;
using Lucene.Net.Index;
using Lucene.Net.Index.Memory;
using Lucene.Net.Queries;
using Lucene.Net.Search.Spans;
using Lucene.Net.Support;
using Lucene.Net.Util;
using System;
using System.Collections;
using System.Collections.Generic;

namespace Lucene.Net.Search.Highlight
{
    /*
	 * Licensed to the Apache Software Foundation (ASF) under one or more
	 * contributor license agreements.  See the NOTICE file distributed with
	 * this work for additional information regarding copyright ownership.
	 * The ASF licenses this file to You under the Apache License, Version 2.0
	 * (the "License"); you may not use this file except in compliance with
	 * the License.  You may obtain a copy of the License at
	 *
	 *     http://www.apache.org/licenses/LICENSE-2.0
	 *
	 * Unless required by applicable law or agreed to in writing, software
	 * distributed under the License is distributed on an "AS IS" BASIS,
	 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
	 * See the License for the specific language governing permissions and
	 * limitations under the License.
	 */

    /// <summary>
    /// Class used to extract <see cref="WeightedSpanTerm"/>s from a <see cref="Query"/> based on whether 
    /// <see cref="Term"/>s from the <see cref="Query"/> are contained in a supplied <see cref="Analysis.TokenStream"/>.
    /// </summary>
    public class WeightedSpanTermExtractor
    {
        private string fieldName;
        private TokenStream tokenStream;
        private string defaultField;
        private bool expandMultiTermQuery;
        private bool cachedTokenStream;
        private bool wrapToCaching = true;
        private int maxDocCharsToAnalyze;
        private AtomicReader internalReader = null;

        public WeightedSpanTermExtractor()
        {
        }

        public WeightedSpanTermExtractor(string defaultField)
        {
            if (defaultField != null)
            {
                this.defaultField = StringHelper.Intern(defaultField);
            }
        }

        /// <summary>
        /// Fills a <see cref="IDictionary{string, WeightedSpanTerm}"/> with <see cref="WeightedSpanTerm"/>s using the terms from the supplied <paramref name="query"/>.
        /// </summary>
        /// <param name="query"><see cref="Query"/> to extract Terms from</param>
        /// <param name="terms">Map to place created <see cref="WeightedSpanTerm"/>s in</param>
        /// <exception cref="System.IO.IOException">If there is a low-level I/O error</exception>
        protected virtual void Extract(Query query, IDictionary<string, WeightedSpanTerm> terms)
        {
            if (query is BooleanQuery)
            {
                IList<BooleanClause> queryClauses = ((BooleanQuery)query).GetClauses();

                for (int i = 0; i < queryClauses.Count; i++)
                {
                    if (!queryClauses[i].Prohibited)
                    {
                        Extract(queryClauses[i].Query, terms);
                    }
                }
            }
            else if (query is PhraseQuery)
            {
                PhraseQuery phraseQuery = (PhraseQuery)query;
                Term[] phraseQueryTerms = phraseQuery.Terms;
                SpanQuery[] clauses = new SpanQuery[phraseQueryTerms.Length];
                for (int i = 0; i < phraseQueryTerms.Length; i++)
                {
                    clauses[i] = new SpanTermQuery(phraseQueryTerms[i]);
                }
                int slop = phraseQuery.Slop;
                int[] positions = phraseQuery.Positions;
                // add largest position increment to slop
                if (positions.Length > 0)
                {
                    int lastPos = positions[0];
                    int largestInc = 0;
                    int sz = positions.Length;
                    for (int i = 1; i < sz; i++)
                    {
                        int pos = positions[i];
                        int inc = pos - lastPos;
                        if (inc > largestInc)
                        {
                            largestInc = inc;
                        }
                        lastPos = pos;
                    }
                    if (largestInc > 1)
                    {
                        slop += largestInc;
                    }
                }

                bool inorder = slop == 0;

                SpanNearQuery sp = new SpanNearQuery(clauses, slop, inorder);
                sp.Boost = query.Boost;
                ExtractWeightedSpanTerms(terms, sp);
            }
            else if (query is TermQuery)
            {
                ExtractWeightedTerms(terms, query);
            }
            else if (query is SpanQuery)
            {
                ExtractWeightedSpanTerms(terms, (SpanQuery)query);
            }
            else if (query is FilteredQuery)
            {
                Extract(((FilteredQuery)query).Query, terms);
            }
            else if (query is ConstantScoreQuery)
            {
                Query q = ((ConstantScoreQuery)query).Query;
                if (q != null)
                {
                    Extract(q, terms);
                }
            }
            else if (query is CommonTermsQuery)
            {
                // specialized since rewriting would change the result query 
                // this query is TermContext sensitive.
                ExtractWeightedTerms(terms, query);
            } 
            else if (query is DisjunctionMaxQuery)
            {
                foreach (var q in ((DisjunctionMaxQuery)query))
                {
                    Extract(q, terms);
                }
            }
            else if (query is MultiPhraseQuery)
            {
                MultiPhraseQuery mpq = (MultiPhraseQuery) query;
                IList<Term[]> termArrays = mpq.TermArrays;
                int[] positions = mpq.Positions;
                if (positions.Length > 0)
                {

                    int maxPosition = positions[positions.Length - 1];
                    for (int i = 0; i < positions.Length - 1; ++i)
                    {
                        if (positions[i] > maxPosition)
                        {
                            maxPosition = positions[i];
                        }
                    }

                    var disjunctLists = new List<SpanQuery>[maxPosition + 1];
                    int distinctPositions = 0;

                    for (int i = 0; i < termArrays.Count; ++i)
                    {
                        Term[] termArray = termArrays[i];
                        List<SpanQuery> disjuncts = disjunctLists[positions[i]];
                        if (disjuncts == null)
                        {
                            disjuncts = (disjunctLists[positions[i]] = new List<SpanQuery>(termArray.Length));
                            ++distinctPositions;
                        }
                        foreach (var term in termArray)
                        {
                            disjuncts.Add(new SpanTermQuery(term));
                        }
                    }

                    int positionGaps = 0;
                    int position = 0;
                    SpanQuery[] clauses = new SpanQuery[distinctPositions];
                    foreach (var disjuncts in disjunctLists)
                    {
                        if (disjuncts != null)
                        {
                            clauses[position++] = new SpanOrQuery(disjuncts.ToArray());
                        }
                        else
                        {
                            ++positionGaps;
                        }
                    }

                    int slop = mpq.Slop;
                    bool inorder = (slop == 0);

                    SpanNearQuery sp = new SpanNearQuery(clauses, slop + positionGaps, inorder);
                    sp.Boost = query.Boost;
                    ExtractWeightedSpanTerms(terms, sp);
                }
            }
            else
            {
                Query origQuery = query;
                if (query is MultiTermQuery)
                {
                    if (!expandMultiTermQuery)
                    {
                        return;
                    }
                    MultiTermQuery copy = (MultiTermQuery) query.Clone();
                    copy.SetRewriteMethod(MultiTermQuery.SCORING_BOOLEAN_QUERY_REWRITE);
                    origQuery = copy;
                }
                IndexReader reader = GetLeafContext().Reader;
                Query rewritten = origQuery.Rewrite(reader);
                if (rewritten != origQuery)
                {
                    // only rewrite once and then flatten again - the rewritten query could have a speacial treatment
                    // if this method is overwritten in a subclass or above in the next recursion
                    Extract(rewritten, terms);
                }
            }
            ExtractUnknownQuery(query, terms);
        }

        protected virtual void ExtractUnknownQuery(Query query,
            IDictionary<string, WeightedSpanTerm> terms)
        {
            // for sub-classing to extract custom queries
        }

        /// <summary>
        /// Fills a <see cref="IDictionary{string, WeightedSpanTerm}"/> with <see cref="WeightedSpanTerm"/>s using the terms from the supplied <see cref="SpanQuery"/>.
        /// </summary>
        /// <param name="terms"><see cref="IDictionary{string, WeightedSpanTerm}"/> to place created <see cref="WeightedSpanTerm"/>s in</param>
        /// <param name="spanQuery"><see cref="SpanQuery"/> to extract Terms from</param>
        /// <exception cref="System.IO.IOException">If there is a low-level I/O error</exception>
        protected virtual void ExtractWeightedSpanTerms(IDictionary<string, WeightedSpanTerm> terms, SpanQuery spanQuery)
        {
            HashSet<string> fieldNames;

            if (fieldName == null)
            {
                fieldNames = new HashSet<string>();
                CollectSpanQueryFields(spanQuery, fieldNames);
            }
            else
            {
                fieldNames = new HashSet<string>();
                fieldNames.Add(fieldName);
            }
            // To support the use of the default field name
            if (defaultField != null)
            {
                fieldNames.Add(defaultField);
            }

            IDictionary<string, SpanQuery> queries = new HashMap<string, SpanQuery>();

            var nonWeightedTerms = Support.Compatibility.SetFactory.CreateHashSet<Term>();
            bool mustRewriteQuery = MustRewriteQuery(spanQuery);
            if (mustRewriteQuery)
            {
                foreach (string field in fieldNames)
                {
                    SpanQuery rewrittenQuery = (SpanQuery)spanQuery.Rewrite(GetLeafContext().Reader);
                    queries[field] = rewrittenQuery;
                    rewrittenQuery.ExtractTerms(nonWeightedTerms);
                }
            }
            else
            {
                spanQuery.ExtractTerms(nonWeightedTerms);
            }

            List<PositionSpan> spanPositions = new List<PositionSpan>();

            foreach (string field in fieldNames)
            {
                SpanQuery q;
                q = mustRewriteQuery ? queries[field] : spanQuery;

                AtomicReaderContext context = GetLeafContext();
                var termContexts = new HashMap<Term, TermContext>();
                TreeSet<Term> extractedTerms = new TreeSet<Term>();
                q.ExtractTerms(extractedTerms);
                foreach (Term term in extractedTerms)
                {
                    termContexts[term] = TermContext.Build(context, term);
                }
                Bits acceptDocs = context.AtomicReader.LiveDocs;
                Spans.Spans spans = q.GetSpans(context, acceptDocs, termContexts);

                // collect span positions
                while (spans.Next())
                {
                    spanPositions.Add(new PositionSpan(spans.Start(), spans.End() - 1));
                }

            }

            if (spanPositions.Count == 0)
            {
                // no spans found
                return;
            }

            foreach (Term queryTerm in nonWeightedTerms)
            {

                if (FieldNameComparator(queryTerm.Field))
                {
                    WeightedSpanTerm weightedSpanTerm;
                    if (!terms.TryGetValue(queryTerm.Text(), out weightedSpanTerm) || weightedSpanTerm == null)
                    {
                        weightedSpanTerm = new WeightedSpanTerm(spanQuery.Boost, queryTerm.Text());
                        weightedSpanTerm.AddPositionSpans(spanPositions);
                        weightedSpanTerm.IsPositionSensitive = true;
                        terms[queryTerm.Text()] = weightedSpanTerm;
                    }
                    else
                    {
                        if (spanPositions.Count > 0)
                        {
                            weightedSpanTerm.AddPositionSpans(spanPositions);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Fills a <see cref="IDictionary{string, WeightedSpanTerm}"/> with <see cref="WeightedSpanTerm"/>s using the terms from 
        /// the supplied <see cref="Search.Spans.SpanQuery"/>.
        /// </summary>
        /// <param name="terms"><see cref="IDictionary{string, WeightedSpanTerm}"/> to place created <see cref="WeightedSpanTerm"/>s in</param>
        /// <param name="query"><see cref="Query"/> to extract Terms from</param>
        /// <exception cref="System.IO.IOException">If there is a low-level I/O error</exception>
        protected virtual void ExtractWeightedTerms(IDictionary<string, WeightedSpanTerm> terms, Query query)
        {
            var nonWeightedTerms = Support.Compatibility.SetFactory.CreateHashSet<Term>();
            query.ExtractTerms(nonWeightedTerms);

            foreach (Term queryTerm in nonWeightedTerms)
            {

                if (FieldNameComparator(queryTerm.Field))
                {
                    WeightedSpanTerm weightedSpanTerm = new WeightedSpanTerm(query.Boost, queryTerm.Text());
                    terms[queryTerm.Text()] = weightedSpanTerm;
                }
            }
        }

        /// <summary>
        /// Necessary to implement matches for queries against <see cref="defaultField"/>
        /// </summary>
        protected virtual bool FieldNameComparator(string fieldNameToCheck)
        {
            bool rv = fieldName == null || fieldName.Equals(fieldNameToCheck)
                      || fieldNameToCheck.Equals(defaultField);
            return rv;
        }

        protected virtual AtomicReaderContext GetLeafContext()
        {
            if (internalReader == null)
            {
                if (wrapToCaching && !(tokenStream is CachingTokenFilter))
                {
                    tokenStream = new CachingTokenFilter(new OffsetLimitTokenFilter(tokenStream, maxDocCharsToAnalyze));
                    cachedTokenStream = true;
                }
                MemoryIndex indexer = new MemoryIndex(true);
                indexer.AddField(DelegatingAtomicReader.FIELD_NAME, tokenStream);
                tokenStream.Reset();
                IndexSearcher searcher = indexer.CreateSearcher();
                // MEM index has only atomic ctx
                var reader = ((AtomicReaderContext) searcher.TopReaderContext).AtomicReader;
                internalReader = new DelegatingAtomicReader(reader);
            }
            return internalReader.AtomicContext;
        }

        /// <summary>
        /// This reader will just delegate every call to a single field in the wrapped
        /// <see cref="AtomicReader"/>. This way we only need to build this field once rather than
        /// N-Times
        /// </summary>
        internal sealed class DelegatingAtomicReader : FilterAtomicReader
        {
            public static string FIELD_NAME = "shadowed_field";

            internal DelegatingAtomicReader(AtomicReader reader) : base(reader) { }

            public override FieldInfos FieldInfos
            {
                get
                {
                    throw new NotSupportedException();
                }
            }

            public override Fields Fields
            {
                get
                {
                    return new DelegatingFilterFields(base.Fields);
                }
            }

            private class DelegatingFilterFields : FilterFields
            {
                public DelegatingFilterFields(Fields fields) : base(fields) { }

                public override Terms Terms(string field)
                {
                    return base.Terms(DelegatingAtomicReader.FIELD_NAME);
                }

                public override IEnumerator<string> GetEnumerator()
                {
                    var list = new List<string> { DelegatingAtomicReader.FIELD_NAME };
                    return list.GetEnumerator();
                }

                public override int Size
                {
                    get { return 1; }
                }
            }

            public override NumericDocValues GetNumericDocValues(string field)
            {
                return base.GetNumericDocValues(FIELD_NAME);
            }

            public override BinaryDocValues GetBinaryDocValues(string field)
            {
                return base.GetBinaryDocValues(FIELD_NAME);
            }
           
            public override SortedDocValues GetSortedDocValues(string field)
            {
                return base.GetSortedDocValues(FIELD_NAME);
            }

            public override NumericDocValues GetNormValues(string field)
            {
                return base.GetNormValues(FIELD_NAME);
            }

            public override Bits GetDocsWithField(string field)
            {
                return base.GetDocsWithField(FIELD_NAME);
            }
        }

        /// <summary>
        /// Creates an <see cref="IDictionary{string, WeightedSpanTerm}"/> from the given <see cref="Query"/> and <see cref="Analysis.TokenStream"/>.
        /// </summary>
        /// <param name="query"><see cref="Query"/> that caused hit</param>
        /// <param name="tokenStream"><see cref="Analysis.TokenStream"/> of text to be highlighted</param>
        /// <returns>Map containing <see cref="WeightedSpanTerm"/>s</returns>
        /// <exception cref="System.IO.IOException">If there is a low-level I/O error</exception>
        public virtual IDictionary<string, WeightedSpanTerm> GetWeightedSpanTerms(Query query, TokenStream tokenStream)
        {
            return GetWeightedSpanTerms(query, tokenStream, null);
        }


        /// <summary>
        /// Creates an <see cref="IDictionary{string, WeightedSpanTerm}"/> from the given <see cref="Query"/> and <see cref="Analysis.TokenStream"/>.
        /// </summary>
        /// <param name="query"><see cref="Query"/> that caused hit</param>
        /// <param name="tokenStream"><see cref="Analysis.TokenStream"/> of text to be highlighted</param>
        /// <param name="fieldName">restricts Term's used based on field name</param>
        /// <returns>Map containing <see cref="WeightedSpanTerm"/>s</returns>
        /// <exception cref="System.IO.IOException">If there is a low-level I/O error</exception>
        public virtual IDictionary<string, WeightedSpanTerm> GetWeightedSpanTerms(Query query, TokenStream tokenStream,
                                                                          string fieldName)
        {
            if (fieldName != null)
            {
                this.fieldName = StringHelper.Intern(fieldName);
            }
            else
            {
                this.fieldName = null;
            }

            IDictionary<string, WeightedSpanTerm> terms = new PositionCheckingMap<string>();
            this.tokenStream = tokenStream;
            try
            {
                Extract(query, terms);
            }
            finally
            {
                IOUtils.Close(internalReader);
            }

            return terms;
        }

        /// <summary>
        /// Creates an <see cref="IDictionary{string, WeightedSpanTerm}"/> from the given <see cref="Query"/> and <see cref="Analysis.TokenStream"/>. Uses a supplied
        /// <see cref="IndexReader"/> to properly Weight terms (for gradient highlighting).
        /// </summary>
        /// <param name="query"><see cref="Query"/> that caused hit</param>
        /// <param name="tokenStream"><see cref="Analysis.TokenStream"/> of text to be highlighted</param>
        /// <param name="fieldName">restricts Term's used based on field name</param>
        /// <param name="reader">to use for scoring</param>
        /// <returns>Map of <see cref="WeightedSpanTerm"/>s with quasi tf/idf scores</returns>
        /// <exception cref="System.IO.IOException">If there is a low-level I/O error</exception>
        public virtual IDictionary<string, WeightedSpanTerm> GetWeightedSpanTermsWithScores(
            Query query, TokenStream tokenStream, string fieldName, IndexReader reader)
        {
            this.fieldName = fieldName == null ? null : StringHelper.Intern(fieldName);

            this.tokenStream = tokenStream;

            IDictionary<string, WeightedSpanTerm> terms = new PositionCheckingMap<string>();
            Extract(query, terms);

            int totalNumDocs = reader.MaxDoc;
            var weightedTerms = terms.Keys;

            try
            {
                foreach (var wt in weightedTerms)
                {
                    WeightedSpanTerm weightedSpanTerm;
                    terms.TryGetValue(wt, out weightedSpanTerm);
                    int docFreq = reader.DocFreq(new Term(fieldName, weightedSpanTerm.Term));
                    // IDF algorithm taken from DefaultSimilarity class
                    float idf = (float)(Math.Log((float)totalNumDocs / (double)(docFreq + 1)) + 1.0);
                    weightedSpanTerm.Weight *= idf;
                }
            }
            finally
            {
                IOUtils.Close(internalReader);
            }

            return terms;
        }

        protected virtual void CollectSpanQueryFields(SpanQuery spanQuery, ISet<string> fieldNames)
        {
            if (spanQuery is FieldMaskingSpanQuery)
            {
                CollectSpanQueryFields(((FieldMaskingSpanQuery)spanQuery).MaskedQuery, fieldNames);
            }
            else if (spanQuery is SpanFirstQuery)
            {
                CollectSpanQueryFields(((SpanFirstQuery)spanQuery).Match, fieldNames);
            }
            else if (spanQuery is SpanNearQuery)
            {
                foreach (SpanQuery clause in ((SpanNearQuery)spanQuery).Clauses)
                {
                    CollectSpanQueryFields(clause, fieldNames);
                }
            }
            else if (spanQuery is SpanNotQuery)
            {
                CollectSpanQueryFields(((SpanNotQuery)spanQuery).Include, fieldNames);
            }
            else if (spanQuery is SpanOrQuery)
            {
                foreach (SpanQuery clause in ((SpanOrQuery)spanQuery).Clauses)
                {
                    CollectSpanQueryFields(clause, fieldNames);
                }
            }
            else
            {
                fieldNames.Add(spanQuery.Field);
            }
        }

        protected virtual bool MustRewriteQuery(SpanQuery spanQuery)
        {
            if (!expandMultiTermQuery)
            {
                return false; // Will throw NotImplementedException in case of a SpanRegexQuery.
            }
            else if (spanQuery is FieldMaskingSpanQuery)
            {
                return MustRewriteQuery(((FieldMaskingSpanQuery)spanQuery).MaskedQuery);
            }
            else if (spanQuery is SpanFirstQuery)
            {
                return MustRewriteQuery(((SpanFirstQuery)spanQuery).Match);
            }
            else if (spanQuery is SpanNearQuery)
            {
                foreach (SpanQuery clause in ((SpanNearQuery)spanQuery).Clauses)
                {
                    if (MustRewriteQuery(clause))
                    {
                        return true;
                    }
                }
                return false;
            }
            else if (spanQuery is SpanNotQuery)
            {
                SpanNotQuery spanNotQuery = (SpanNotQuery)spanQuery;
                return MustRewriteQuery(spanNotQuery.Include) || MustRewriteQuery(spanNotQuery.Exclude);
            }
            else if (spanQuery is SpanOrQuery)
            {
                foreach (SpanQuery clause in ((SpanOrQuery)spanQuery).Clauses)
                {
                    if (MustRewriteQuery(clause))
                    {
                        return true;
                    }
                }
                return false;
            }
            else if (spanQuery is SpanTermQuery)
            {
                return false;
            }
            else
            {
                return true;
            }
        }


        /// <summary>
        /// This class makes sure that if both position sensitive and insensitive
        /// versions of the same term are added, the position insensitive one wins.
        /// </summary>
        /// <typeparam name="K"></typeparam>
        // LUCENENET NOTE: Unfortunately, members of Dictionary{TKey, TValue} are not virtual,
        // so we need to implement IDictionary{TKey, TValue} instead.
        protected class PositionCheckingMap<K> : IDictionary<K, WeightedSpanTerm>
        {
            private readonly IDictionary<K, WeightedSpanTerm> wrapped = new Dictionary<K, WeightedSpanTerm>();

            public WeightedSpanTerm this[K key]
            {
                get
                {
                    return wrapped[key];
                }

                set
                {
                    WeightedSpanTerm prev = null;
                    wrapped.TryGetValue(key, out prev);
                    wrapped[key] = value;

                    if (prev == null) return;

                    WeightedSpanTerm prevTerm = prev;
                    WeightedSpanTerm newTerm = value;
                    if (!prevTerm.IsPositionSensitive)
                    {
                        newTerm.IsPositionSensitive = false;
                    }
                }
            }

            public int Count
            {
                get
                {
                    return wrapped.Count;
                }
            }

            public bool IsReadOnly
            {
                get
                {
                    return false;
                }
            }

            public ICollection<K> Keys
            {
                get
                {
                    return wrapped.Keys;
                }
            }

            public ICollection<WeightedSpanTerm> Values
            {
                get
                {
                    return wrapped.Values;
                }
            }

            public void Add(KeyValuePair<K, WeightedSpanTerm> item)
            {
                this[item.Key] = item.Value;
            }

            public void Add(K key, WeightedSpanTerm value)
            {
                this[key] = value;
            }

            public void Clear()
            {
                wrapped.Clear();
            }

            public bool Contains(KeyValuePair<K, WeightedSpanTerm> item)
            {
                return wrapped.Contains(item);
            }

            public bool ContainsKey(K key)
            {
                return wrapped.ContainsKey(key);
            }

            public void CopyTo(KeyValuePair<K, WeightedSpanTerm>[] array, int arrayIndex)
            {
                wrapped.CopyTo(array, arrayIndex);
            }

            public IEnumerator<KeyValuePair<K, WeightedSpanTerm>> GetEnumerator()
            {
                return wrapped.GetEnumerator();
            }

            public bool Remove(KeyValuePair<K, WeightedSpanTerm> item)
            {
                return wrapped.Remove(item);
            }

            public bool Remove(K key)
            {
                return wrapped.Remove(key);
            }

            public bool TryGetValue(K key, out WeightedSpanTerm value)
            {
                return wrapped.TryGetValue(key, out value);
            }

            IEnumerator IEnumerable.GetEnumerator()
            {
                return GetEnumerator();
            }
        }

        public virtual bool ExpandMultiTermQuery
        {
            set { expandMultiTermQuery = value; }
            get { return expandMultiTermQuery; }
        }

        public virtual bool IsCachedTokenStream
        {
            get { return cachedTokenStream; }
        }

        public virtual TokenStream TokenStream
        {
            get { return tokenStream; }
        }

        /// <summary>
        /// By default, <see cref="Analysis.TokenStream"/>s that are not of the type
        /// <see cref="CachingTokenFilter"/> are wrapped in a <see cref="CachingTokenFilter"/> to
        /// <see cref="Analysis.TokenStream"/> impl and you don't want it to be wrapped, set this to
        /// false.
        /// </summary>
        public virtual void SetWrapIfNotCachingTokenFilter(bool wrap)
        {
            this.wrapToCaching = wrap;
        }

        protected internal void SetMaxDocCharsToAnalyze(int maxDocCharsToAnalyze)
        {
            this.maxDocCharsToAnalyze = maxDocCharsToAnalyze;
        }
    }
}