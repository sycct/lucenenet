using Lucene.Net.Support;
using System;
using System.Collections.Generic;
using System.Text;

/*
 * dk.brics.automaton
 *
 * Copyright (c) 2001-2009 Anders Moeller
 * All rights reserved.
 *
 * Redistribution and use in source and binary forms, with or without
 * modification, are permitted provided that the following conditions
 * are met:
 * 1. Redistributions of source code must retain the above copyright
 *    notice, this list of conditions and the following disclaimer.
 * 2. Redistributions in binary form must reproduce the above copyright
 *    notice, this list of conditions and the following disclaimer in the
 *    documentation and/or other materials provided with the distribution.
 * 3. The name of the author may not be used to endorse or promote products
 *    derived from this software without specific prior written permission.
 *
 * this SOFTWARE IS PROVIDED BY THE AUTHOR ``AS IS'' AND ANY EXPRESS OR
 * IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED WARRANTIES
 * OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE DISCLAIMED.
 * IN NO EVENT SHALL THE AUTHOR BE LIABLE FOR ANY DIRECT, INDIRECT,
 * INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES (INCLUDING, BUT
 * NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES; LOSS OF USE,
 * DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND ON ANY
 * THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
 * (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF
 * this SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
 */

namespace Lucene.Net.Util.Automaton
{
    // LUCENENET specific - converted constants from RegExp
    // into a flags enum.
    [Flags]
    public enum RegExpSyntax
    {
        /// <summary>
        /// Syntax flag, enables intersection (<c>&amp;</c>).
        /// </summary>
        INTERSECTION = 0x0001,

        /// <summary>
        /// Syntax flag, enables complement (<c>~</c>).
        /// </summary>
        COMPLEMENT = 0x0002,

        /// <summary>
        /// Syntax flag, enables empty language (<c>#</c>).
        /// </summary>
        EMPTY = 0x0004,

        /// <summary>
        /// Syntax flag, enables anystring (<c>@</c>).
        /// </summary>
        ANYSTRING = 0x0008,

        /// <summary>
        /// Syntax flag, enables named automata (<c>&lt;</c>identifier<c>&gt;</c>).
        /// </summary>
        AUTOMATON = 0x0010,

        /// <summary>
        /// Syntax flag, enables numerical intervals (
        /// <c>&lt;<i>n</i>-<i>m</i>&gt;</c>).
        /// </summary>
        INTERVAL = 0x0020,

        /// <summary>
        /// Syntax flag, enables all optional regexp syntax.
        /// </summary>
        ALL = 0xffff,

        /// <summary>
        /// Syntax flag, enables no optional regexp syntax.
        /// </summary>
        NONE = 0x0000
    }


    /// <summary>
    /// Regular Expression extension to <code>Automaton</code>.
    /// <p>
    /// Regular expressions are built from the following abstract syntax:
    /// <p>
    /// <table border=0>
    /// <tr>
    /// <td><i>regexp</i></td>
    /// <td>::=</td>
    /// <td><i>unionexp</i></td>
    /// <td></td>
    /// <td></td>
    /// </tr>
    /// <tr>
    /// <td></td>
    /// <td>|</td>
    /// <td></td>
    /// <td></td>
    /// <td></td>
    /// </tr>
    ///
    /// <tr>
    /// <td><i>unionexp</i></td>
    /// <td>::=</td>
    /// <td><i>interexp</i>&nbsp;<tt><b>|</b></tt>&nbsp;<i>unionexp</i></td>
    /// <td>(union)</td>
    /// <td></td>
    /// </tr>
    /// <tr>
    /// <td></td>
    /// <td>|</td>
    /// <td><i>interexp</i></td>
    /// <td></td>
    /// <td></td>
    /// </tr>
    ///
    /// <tr>
    /// <td><i>interexp</i></td>
    /// <td>::=</td>
    /// <td><i>concatexp</i>&nbsp;<tt><b>&amp;</b></tt>&nbsp;<i>interexp</i></td>
    /// <td>(intersection)</td>
    /// <td><small>[OPTIONAL]</small></td>
    /// </tr>
    /// <tr>
    /// <td></td>
    /// <td>|</td>
    /// <td><i>concatexp</i></td>
    /// <td></td>
    /// <td></td>
    /// </tr>
    ///
    /// <tr>
    /// <td><i>concatexp</i></td>
    /// <td>::=</td>
    /// <td><i>repeatexp</i>&nbsp;<i>concatexp</i></td>
    /// <td>(concatenation)</td>
    /// <td></td>
    /// </tr>
    /// <tr>
    /// <td></td>
    /// <td>|</td>
    /// <td><i>repeatexp</i></td>
    /// <td></td>
    /// <td></td>
    /// </tr>
    ///
    /// <tr>
    /// <td><i>repeatexp</i></td>
    /// <td>::=</td>
    /// <td><i>repeatexp</i>&nbsp;<tt><b>?</b></tt></td>
    /// <td>(zero or one occurrence)</td>
    /// <td></td>
    /// </tr>
    /// <tr>
    /// <td></td>
    /// <td>|</td>
    /// <td><i>repeatexp</i>&nbsp;<tt><b>*</b></tt></td>
    /// <td>(zero or more occurrences)</td>
    /// <td></td>
    /// </tr>
    /// <tr>
    /// <td></td>
    /// <td>|</td>
    /// <td><i>repeatexp</i>&nbsp;<tt><b>+</b></tt></td>
    /// <td>(one or more occurrences)</td>
    /// <td></td>
    /// </tr>
    /// <tr>
    /// <td></td>
    /// <td>|</td>
    /// <td><i>repeatexp</i>&nbsp;<tt><b>{</b><i>n</i><b>}</b></tt></td>
    /// <td>(<tt><i>n</i></tt> occurrences)</td>
    /// <td></td>
    /// </tr>
    /// <tr>
    /// <td></td>
    /// <td>|</td>
    /// <td><i>repeatexp</i>&nbsp;<tt><b>{</b><i>n</i><b>,}</b></tt></td>
    /// <td>(<tt><i>n</i></tt> or more occurrences)</td>
    /// <td></td>
    /// </tr>
    /// <tr>
    /// <td></td>
    /// <td>|</td>
    /// <td><i>repeatexp</i>&nbsp;<tt><b>{</b><i>n</i><b>,</b><i>m</i><b>}</b></tt></td>
    /// <td>(<tt><i>n</i></tt> to <tt><i>m</i></tt> occurrences, including both)</td>
    /// <td></td>
    /// </tr>
    /// <tr>
    /// <td></td>
    /// <td>|</td>
    /// <td><i>complexp</i></td>
    /// <td></td>
    /// <td></td>
    /// </tr>
    ///
    /// <tr>
    /// <td><i>complexp</i></td>
    /// <td>::=</td>
    /// <td><tt><b>~</b></tt>&nbsp;<i>complexp</i></td>
    /// <td>(complement)</td>
    /// <td><small>[OPTIONAL]</small></td>
    /// </tr>
    /// <tr>
    /// <td></td>
    /// <td>|</td>
    /// <td><i>charclassexp</i></td>
    /// <td></td>
    /// <td></td>
    /// </tr>
    ///
    /// <tr>
    /// <td><i>charclassexp</i></td>
    /// <td>::=</td>
    /// <td><tt><b>[</b></tt>&nbsp;<i>charclasses</i>&nbsp;<tt><b>]</b></tt></td>
    /// <td>(character class)</td>
    /// <td></td>
    /// </tr>
    /// <tr>
    /// <td></td>
    /// <td>|</td>
    /// <td><tt><b>[^</b></tt>&nbsp;<i>charclasses</i>&nbsp;<tt><b>]</b></tt></td>
    /// <td>(negated character class)</td>
    /// <td></td>
    /// </tr>
    /// <tr>
    /// <td></td>
    /// <td>|</td>
    /// <td><i>simpleexp</i></td>
    /// <td></td>
    /// <td></td>
    /// </tr>
    ///
    /// <tr>
    /// <td><i>charclasses</i></td>
    /// <td>::=</td>
    /// <td><i>charclass</i>&nbsp;<i>charclasses</i></td>
    /// <td></td>
    /// <td></td>
    /// </tr>
    /// <tr>
    /// <td></td>
    /// <td>|</td>
    /// <td><i>charclass</i></td>
    /// <td></td>
    /// <td></td>
    /// </tr>
    ///
    /// <tr>
    /// <td><i>charclass</i></td>
    /// <td>::=</td>
    /// <td><i>charexp</i>&nbsp;<tt><b>-</b></tt>&nbsp;<i>charexp</i></td>
    /// <td>(character range, including end-points)</td>
    /// <td></td>
    /// </tr>
    /// <tr>
    /// <td></td>
    /// <td>|</td>
    /// <td><i>charexp</i></td>
    /// <td></td>
    /// <td></td>
    /// </tr>
    ///
    /// <tr>
    /// <td><i>simpleexp</i></td>
    /// <td>::=</td>
    /// <td><i>charexp</i></td>
    /// <td></td>
    /// <td></td>
    /// </tr>
    /// <tr>
    /// <td></td>
    /// <td>|</td>
    /// <td><tt><b>.</b></tt></td>
    /// <td>(any single character)</td>
    /// <td></td>
    /// </tr>
    /// <tr>
    /// <td></td>
    /// <td>|</td>
    /// <td><tt><b>#</b></tt></td>
    /// <td>(the empty language)</td>
    /// <td><small>[OPTIONAL]</small></td>
    /// </tr>
    /// <tr>
    /// <td></td>
    /// <td>|</td>
    /// <td><tt><b>@</b></tt></td>
    /// <td>(any string)</td>
    /// <td><small>[OPTIONAL]</small></td>
    /// </tr>
    /// <tr>
    /// <td></td>
    /// <td>|</td>
    /// <td><tt><b>"</b></tt>&nbsp;&lt;Unicode string without double-quotes&gt;&nbsp; <tt><b>"</b></tt></td>
    /// <td>(a string)</td>
    /// <td></td>
    /// </tr>
    /// <tr>
    /// <td></td>
    /// <td>|</td>
    /// <td><tt><b>(</b></tt>&nbsp;<tt><b>)</b></tt></td>
    /// <td>(the empty string)</td>
    /// <td></td>
    /// </tr>
    /// <tr>
    /// <td></td>
    /// <td>|</td>
    /// <td><tt><b>(</b></tt>&nbsp;<i>unionexp</i>&nbsp;<tt><b>)</b></tt></td>
    /// <td>(precedence override)</td>
    /// <td></td>
    /// </tr>
    /// <tr>
    /// <td></td>
    /// <td>|</td>
    /// <td><tt><b>&lt;</b></tt>&nbsp;&lt;identifier&gt;&nbsp;<tt><b>&gt;</b></tt></td>
    /// <td>(named automaton)</td>
    /// <td><small>[OPTIONAL]</small></td>
    /// </tr>
    /// <tr>
    /// <td></td>
    /// <td>|</td>
    /// <td><tt><b>&lt;</b><i>n</i>-<i>m</i><b>&gt;</b></tt></td>
    /// <td>(numerical interval)</td>
    /// <td><small>[OPTIONAL]</small></td>
    /// </tr>
    ///
    /// <tr>
    /// <td><i>charexp</i></td>
    /// <td>::=</td>
    /// <td>&lt;Unicode character&gt;</td>
    /// <td>(a single non-reserved character)</td>
    /// <td></td>
    /// </tr>
    /// <tr>
    /// <td></td>
    /// <td>|</td>
    /// <td><tt><b>\</b></tt>&nbsp;&lt;Unicode character&gt;&nbsp;</td>
    /// <td>(a single character)</td>
    /// <td></td>
    /// </tr>
    /// </table>
    /// <p>
    /// The productions marked <small>[OPTIONAL]</small> are only allowed if
    /// specified by the syntax flags passed to the <code>RegExp</code> constructor.
    /// The reserved characters used in the (enabled) syntax must be escaped with
    /// backslash (<tt><b>\</b></tt>) or double-quotes (<tt><b>"..."</b></tt>). (In
    /// contrast to other regexp syntaxes, this is required also in character
    /// classes.) Be aware that dash (<tt><b>-</b></tt>) has a special meaning in
    /// <i>charclass</i> expressions. An identifier is a string not containing right
    /// angle bracket (<tt><b>&gt;</b></tt>) or dash (<tt><b>-</b></tt>). Numerical
    /// intervals are specified by non-negative decimal integers and include both end
    /// points, and if <tt><i>n</i></tt> and <tt><i>m</i></tt> have the same number
    /// of digits, then the conforming strings must have that length (i.e. prefixed
    /// by 0's).
    ///
    /// @lucene.experimental
    /// </summary>
    public class RegExp
    {
        internal enum Kind
        {
            REGEXP_UNION,
            REGEXP_CONCATENATION,
            REGEXP_INTERSECTION,
            REGEXP_OPTIONAL,
            REGEXP_REPEAT,
            REGEXP_REPEAT_MIN,
            REGEXP_REPEAT_MINMAX,
            REGEXP_COMPLEMENT,
            REGEXP_CHAR,
            REGEXP_CHAR_RANGE,
            REGEXP_ANYCHAR,
            REGEXP_EMPTY,
            REGEXP_STRING,
            REGEXP_ANYSTRING,
            REGEXP_AUTOMATON,
            REGEXP_INTERVAL
        }

        // LUCENENET specific - made flags into their own [Flags] enum named RegExpSyntax and de-nested from this type


        private static bool allow_mutation = false;

        internal Kind kind;
        internal RegExp exp1, exp2;
        internal string s;
        internal int c;
        internal int min, max, digits;
        internal int from, to;

        internal string b;
        internal RegExpSyntax flags;
        internal int pos;

        internal RegExp()
        {
        }

        /// <summary>
        /// Constructs new <see cref="RegExp"/> from a string. Same as
        /// <c>RegExp(s, RegExpSyntax.ALL)</c>.
        /// </summary>
        /// <param name="s"> regexp string </param>
        /// <exception cref="ArgumentException"> if an error occured while parsing the
        ///              regular expression </exception>
        public RegExp(string s)
            : this(s, RegExpSyntax.ALL)
        {
        }

        /// <summary>
        /// Constructs new <see cref="RegExp"/> from a string.
        /// </summary>
        /// <param name="s"> regexp string </param>
        /// <param name="syntax_flags"> boolean 'or' of optional <see cref="RegExpSyntax"/> constructs to be
        ///          enabled </param>
        /// <exception cref="ArgumentException"> if an error occured while parsing the
        ///              regular expression </exception>
        public RegExp(string s, RegExpSyntax syntax_flags)
        {
            b = s;
            flags = syntax_flags;
            RegExp e;
            if (s.Length == 0)
            {
                e = MakeString("");
            }
            else
            {
                e = ParseUnionExp();
                if (pos < b.Length)
                {
                    throw new System.ArgumentException("end-of-string expected at position " + pos);
                }
            }
            kind = e.kind;
            exp1 = e.exp1;
            exp2 = e.exp2;
            this.s = e.s;
            c = e.c;
            min = e.min;
            max = e.max;
            digits = e.digits;
            from = e.from;
            to = e.to;
            b = null;
        }

        /// <summary>
        /// Constructs new <code>Automaton</code> from this <code>RegExp</code>. Same
        /// as <code>toAutomaton(null)</code> (empty automaton map).
        /// </summary>
        public virtual Automaton ToAutomaton()
        {
            return ToAutomatonAllowMutate(null, null);
        }

        /// <summary>
        /// Constructs new <code>Automaton</code> from this <code>RegExp</code>. The
        /// constructed automaton is minimal and deterministic and has no transitions
        /// to dead states.
        /// </summary>
        /// <param name="automaton_provider"> provider of automata for named identifiers </param>
        /// <exception cref="IllegalArgumentException"> if this regular expression uses a named
        ///              identifier that is not available from the automaton provider </exception>
        public virtual Automaton ToAutomaton(IAutomatonProvider automaton_provider)
        {
            return ToAutomatonAllowMutate(null, automaton_provider);
        }

        /// <summary>
        /// Constructs new <code>Automaton</code> from this <code>RegExp</code>. The
        /// constructed automaton is minimal and deterministic and has no transitions
        /// to dead states.
        /// </summary>
        /// <param name="automata"> a map from automaton identifiers to automata (of type
        ///          <code>Automaton</code>). </param>
        /// <exception cref="IllegalArgumentException"> if this regular expression uses a named
        ///              identifier that does not occur in the automaton map </exception>
        public virtual Automaton ToAutomaton(IDictionary<string, Automaton> automata)
        {
            return ToAutomatonAllowMutate(automata, null);
        }

        /// <summary>
        /// Sets or resets allow mutate flag. If this flag is set, then automata
        /// construction uses mutable automata, which is slightly faster but not thread
        /// safe. By default, the flag is not set.
        /// </summary>
        /// <param name="flag"> if true, the flag is set </param>
        /// <returns> previous value of the flag </returns>
        public virtual bool SetAllowMutate(bool flag)
        {
            bool b = allow_mutation;
            allow_mutation = flag;
            return b;
        }

        private Automaton ToAutomatonAllowMutate(IDictionary<string, Automaton> automata, IAutomatonProvider automaton_provider)
        {
            bool b = false;
            if (allow_mutation) // thread unsafe
            {
                b = Automaton.SetAllowMutate(true);
            }
            Automaton a = ToAutomaton(automata, automaton_provider);
            if (allow_mutation)
            {
                Automaton.SetAllowMutate(b);
            }
            return a;
        }

        private Automaton ToAutomaton(IDictionary<string, Automaton> automata, IAutomatonProvider automaton_provider)
        {
            IList<Automaton> list;
            Automaton a = null;
            switch (kind)
            {
                case Kind.REGEXP_UNION:
                    list = new List<Automaton>();
                    FindLeaves(exp1, Kind.REGEXP_UNION, list, automata, automaton_provider);
                    FindLeaves(exp2, Kind.REGEXP_UNION, list, automata, automaton_provider);
                    a = BasicOperations.Union(list);
                    MinimizationOperations.Minimize(a);
                    break;

                case Kind.REGEXP_CONCATENATION:
                    list = new List<Automaton>();
                    FindLeaves(exp1, Kind.REGEXP_CONCATENATION, list, automata, automaton_provider);
                    FindLeaves(exp2, Kind.REGEXP_CONCATENATION, list, automata, automaton_provider);
                    a = BasicOperations.Concatenate(list);
                    MinimizationOperations.Minimize(a);
                    break;

                case Kind.REGEXP_INTERSECTION:
                    a = exp1.ToAutomaton(automata, automaton_provider).Intersection(exp2.ToAutomaton(automata, automaton_provider));
                    MinimizationOperations.Minimize(a);
                    break;

                case Kind.REGEXP_OPTIONAL:
                    a = exp1.ToAutomaton(automata, automaton_provider).Optional();
                    MinimizationOperations.Minimize(a);
                    break;

                case Kind.REGEXP_REPEAT:
                    a = exp1.ToAutomaton(automata, automaton_provider).Repeat();
                    MinimizationOperations.Minimize(a);
                    break;

                case Kind.REGEXP_REPEAT_MIN:
                    a = exp1.ToAutomaton(automata, automaton_provider).Repeat(min);
                    MinimizationOperations.Minimize(a);
                    break;

                case Kind.REGEXP_REPEAT_MINMAX:
                    a = exp1.ToAutomaton(automata, automaton_provider).Repeat(min, max);
                    MinimizationOperations.Minimize(a);
                    break;

                case Kind.REGEXP_COMPLEMENT:
                    a = exp1.ToAutomaton(automata, automaton_provider).Complement();
                    MinimizationOperations.Minimize(a);
                    break;

                case Kind.REGEXP_CHAR:
                    a = BasicAutomata.MakeChar(c);
                    break;

                case Kind.REGEXP_CHAR_RANGE:
                    a = BasicAutomata.MakeCharRange(from, to);
                    break;

                case Kind.REGEXP_ANYCHAR:
                    a = BasicAutomata.MakeAnyChar();
                    break;

                case Kind.REGEXP_EMPTY:
                    a = BasicAutomata.MakeEmpty();
                    break;

                case Kind.REGEXP_STRING:
                    a = BasicAutomata.MakeString(s);
                    break;

                case Kind.REGEXP_ANYSTRING:
                    a = BasicAutomata.MakeAnyString();
                    break;

                case Kind.REGEXP_AUTOMATON:
                    Automaton aa = null;
                    if (automata != null)
                    {
                        aa = automata[s];
                    }
                    if (aa == null && automaton_provider != null)
                    {
                        try
                        {
                            aa = automaton_provider.GetAutomaton(s);
                        }
                        catch (System.IO.IOException e)
                        {
                            throw new System.ArgumentException(e.ToString(), e);
                        }
                    }
                    if (aa == null)
                    {
                        throw new System.ArgumentException("'" + s + "' not found");
                    }
                    a = (Automaton)aa.Clone(); // always clone here (ignore allow_mutate)
                    break;

                case Kind.REGEXP_INTERVAL:
                    a = BasicAutomata.MakeInterval(min, max, digits);
                    break;
            }
            return a;
        }

        private void FindLeaves(RegExp exp, Kind kind, IList<Automaton> list, IDictionary<string, Automaton> automata, IAutomatonProvider automaton_provider)
        {
            if (exp.kind == kind)
            {
                FindLeaves(exp.exp1, kind, list, automata, automaton_provider);
                FindLeaves(exp.exp2, kind, list, automata, automaton_provider);
            }
            else
            {
                list.Add(exp.ToAutomaton(automata, automaton_provider));
            }
        }

        /// <summary>
        /// Constructs string from parsed regular expression.
        /// </summary>
        public override string ToString()
        {
            return ToStringBuilder(new StringBuilder()).ToString();
        }

        internal virtual StringBuilder ToStringBuilder(StringBuilder b)
        {
            switch (kind)
            {
                case Kind.REGEXP_UNION:
                    b.Append("(");
                    exp1.ToStringBuilder(b);
                    b.Append("|");
                    exp2.ToStringBuilder(b);
                    b.Append(")");
                    break;

                case Kind.REGEXP_CONCATENATION:
                    exp1.ToStringBuilder(b);
                    exp2.ToStringBuilder(b);
                    break;

                case Kind.REGEXP_INTERSECTION:
                    b.Append("(");
                    exp1.ToStringBuilder(b);
                    b.Append("&");
                    exp2.ToStringBuilder(b);
                    b.Append(")");
                    break;

                case Kind.REGEXP_OPTIONAL:
                    b.Append("(");
                    exp1.ToStringBuilder(b);
                    b.Append(")?");
                    break;

                case Kind.REGEXP_REPEAT:
                    b.Append("(");
                    exp1.ToStringBuilder(b);
                    b.Append(")*");
                    break;

                case Kind.REGEXP_REPEAT_MIN:
                    b.Append("(");
                    exp1.ToStringBuilder(b);
                    b.Append("){").Append(min).Append(",}");
                    break;

                case Kind.REGEXP_REPEAT_MINMAX:
                    b.Append("(");
                    exp1.ToStringBuilder(b);
                    b.Append("){").Append(min).Append(",").Append(max).Append("}");
                    break;

                case Kind.REGEXP_COMPLEMENT:
                    b.Append("~(");
                    exp1.ToStringBuilder(b);
                    b.Append(")");
                    break;

                case Kind.REGEXP_CHAR:
                    b.Append("\\").AppendCodePoint(c);
                    break;

                case Kind.REGEXP_CHAR_RANGE:
                    b.Append("[\\").AppendCodePoint(from).Append("-\\").AppendCodePoint(to).Append("]");
                    break;

                case Kind.REGEXP_ANYCHAR:
                    b.Append(".");
                    break;

                case Kind.REGEXP_EMPTY:
                    b.Append("#");
                    break;

                case Kind.REGEXP_STRING:
                    b.Append("\"").Append(s).Append("\"");
                    break;

                case Kind.REGEXP_ANYSTRING:
                    b.Append("@");
                    break;

                case Kind.REGEXP_AUTOMATON:
                    b.Append("<").Append(s).Append(">");
                    break;

                case Kind.REGEXP_INTERVAL:
                    string s1 = Convert.ToString(min);
                    string s2 = Convert.ToString(max);
                    b.Append("<");
                    if (digits > 0)
                    {
                        for (int i = s1.Length; i < digits; i++)
                        {
                            b.Append('0');
                        }
                    }
                    b.Append(s1).Append("-");
                    if (digits > 0)
                    {
                        for (int i = s2.Length; i < digits; i++)
                        {
                            b.Append('0');
                        }
                    }
                    b.Append(s2).Append(">");
                    break;
            }
            return b;
        }

        /// <summary>
        /// Returns set of automaton identifiers that occur in this regular expression.
        /// </summary>
        public virtual ISet<string> GetIdentifiers()
        {
            ISet<string> set = new EquatableSet<string>();
            GetIdentifiers(set);
            return set;
        }

        internal virtual void GetIdentifiers(ISet<string> set)
        {
            switch (kind)
            {
                case Kind.REGEXP_UNION:
                case Kind.REGEXP_CONCATENATION:
                case Kind.REGEXP_INTERSECTION:
                    exp1.GetIdentifiers(set);
                    exp2.GetIdentifiers(set);
                    break;

                case Kind.REGEXP_OPTIONAL:
                case Kind.REGEXP_REPEAT:
                case Kind.REGEXP_REPEAT_MIN:
                case Kind.REGEXP_REPEAT_MINMAX:
                case Kind.REGEXP_COMPLEMENT:
                    exp1.GetIdentifiers(set);
                    break;

                case Kind.REGEXP_AUTOMATON:
                    set.Add(s);
                    break;

                default:
                    break;
            }
        }

        internal static RegExp MakeUnion(RegExp exp1, RegExp exp2)
        {
            RegExp r = new RegExp();
            r.kind = Kind.REGEXP_UNION;
            r.exp1 = exp1;
            r.exp2 = exp2;
            return r;
        }

        internal static RegExp MakeConcatenation(RegExp exp1, RegExp exp2)
        {
            if ((exp1.kind == Kind.REGEXP_CHAR || exp1.kind == Kind.REGEXP_STRING) && (exp2.kind == Kind.REGEXP_CHAR || exp2.kind == Kind.REGEXP_STRING))
            {
                return MakeString(exp1, exp2);
            }
            RegExp r = new RegExp();
            r.kind = Kind.REGEXP_CONCATENATION;
            if (exp1.kind == Kind.REGEXP_CONCATENATION && (exp1.exp2.kind == Kind.REGEXP_CHAR || exp1.exp2.kind == Kind.REGEXP_STRING) && (exp2.kind == Kind.REGEXP_CHAR || exp2.kind == Kind.REGEXP_STRING))
            {
                r.exp1 = exp1.exp1;
                r.exp2 = MakeString(exp1.exp2, exp2);
            }
            else if ((exp1.kind == Kind.REGEXP_CHAR || exp1.kind == Kind.REGEXP_STRING) && exp2.kind == Kind.REGEXP_CONCATENATION && (exp2.exp1.kind == Kind.REGEXP_CHAR || exp2.exp1.kind == Kind.REGEXP_STRING))
            {
                r.exp1 = MakeString(exp1, exp2.exp1);
                r.exp2 = exp2.exp2;
            }
            else
            {
                r.exp1 = exp1;
                r.exp2 = exp2;
            }
            return r;
        }

        private static RegExp MakeString(RegExp exp1, RegExp exp2)
        {
            StringBuilder b = new StringBuilder();
            if (exp1.kind == Kind.REGEXP_STRING)
            {
                b.Append(exp1.s);
            }
            else
            {
                b.AppendCodePoint(exp1.c);
            }
            if (exp2.kind == Kind.REGEXP_STRING)
            {
                b.Append(exp2.s);
            }
            else
            {
                b.AppendCodePoint(exp2.c);
            }
            return MakeString(b.ToString());
        }

        internal static RegExp MakeIntersection(RegExp exp1, RegExp exp2)
        {
            RegExp r = new RegExp();
            r.kind = Kind.REGEXP_INTERSECTION;
            r.exp1 = exp1;
            r.exp2 = exp2;
            return r;
        }

        internal static RegExp MakeOptional(RegExp exp)
        {
            RegExp r = new RegExp();
            r.kind = Kind.REGEXP_OPTIONAL;
            r.exp1 = exp;
            return r;
        }

        internal static RegExp MakeRepeat(RegExp exp)
        {
            RegExp r = new RegExp();
            r.kind = Kind.REGEXP_REPEAT;
            r.exp1 = exp;
            return r;
        }

        internal static RegExp MakeRepeat(RegExp exp, int min)
        {
            RegExp r = new RegExp();
            r.kind = Kind.REGEXP_REPEAT_MIN;
            r.exp1 = exp;
            r.min = min;
            return r;
        }

        internal static RegExp MakeRepeat(RegExp exp, int min, int max)
        {
            RegExp r = new RegExp();
            r.kind = Kind.REGEXP_REPEAT_MINMAX;
            r.exp1 = exp;
            r.min = min;
            r.max = max;
            return r;
        }

        internal static RegExp MakeComplement(RegExp exp)
        {
            RegExp r = new RegExp();
            r.kind = Kind.REGEXP_COMPLEMENT;
            r.exp1 = exp;
            return r;
        }

        internal static RegExp MakeChar(int c)
        {
            RegExp r = new RegExp();
            r.kind = Kind.REGEXP_CHAR;
            r.c = c;
            return r;
        }

        internal static RegExp MakeCharRange(int from, int to)
        {
            if (from > to)
            {
                throw new System.ArgumentException("invalid range: from (" + from + ") cannot be > to (" + to + ")");
            }
            RegExp r = new RegExp();
            r.kind = Kind.REGEXP_CHAR_RANGE;
            r.from = from;
            r.to = to;
            return r;
        }

        internal static RegExp MakeAnyChar()
        {
            RegExp r = new RegExp();
            r.kind = Kind.REGEXP_ANYCHAR;
            return r;
        }

        internal static RegExp MakeEmpty()
        {
            RegExp r = new RegExp();
            r.kind = Kind.REGEXP_EMPTY;
            return r;
        }

        internal static RegExp MakeString(string s)
        {
            RegExp r = new RegExp();
            r.kind = Kind.REGEXP_STRING;
            r.s = s;
            return r;
        }

        internal static RegExp MakeAnyString()
        {
            RegExp r = new RegExp();
            r.kind = Kind.REGEXP_ANYSTRING;
            return r;
        }

        internal static RegExp MakeAutomaton(string s)
        {
            RegExp r = new RegExp();
            r.kind = Kind.REGEXP_AUTOMATON;
            r.s = s;
            return r;
        }

        internal static RegExp MakeInterval(int min, int max, int digits)
        {
            RegExp r = new RegExp();
            r.kind = Kind.REGEXP_INTERVAL;
            r.min = min;
            r.max = max;
            r.digits = digits;
            return r;
        }

        private bool Peek(string s)
        {
            return More() && s.IndexOf(b.CodePointAt(pos)) != -1;
        }

        private bool Match(int c)
        {
            if (pos >= b.Length)
            {
                return false;
            }
            if (b.CodePointAt(pos) == c)
            {
                pos += Character.CharCount(c);
                return true;
            }
            return false;
        }

        private bool More()
        {
            return pos < b.Length;
        }

        private int Next()
        {
            if (!More())
            {
                throw new System.ArgumentException("unexpected end-of-string");
            }
            int ch = b.CodePointAt(pos);
            pos += Character.CharCount(ch);
            return ch;
        }

        private bool Check(RegExpSyntax flag)
        {
            return (flags & flag) != 0;
        }

        internal RegExp ParseUnionExp()
        {
            RegExp e = ParseInterExp();
            if (Match('|'))
            {
                e = MakeUnion(e, ParseUnionExp());
            }
            return e;
        }

        internal RegExp ParseInterExp()
        {
            RegExp e = ParseConcatExp();
            if (Check(RegExpSyntax.INTERSECTION) && Match('&'))
            {
                e = MakeIntersection(e, ParseInterExp());
            }
            return e;
        }

        internal RegExp ParseConcatExp()
        {
            RegExp e = ParseRepeatExp();
            if (More() && !Peek(")|") && (!Check(RegExpSyntax.INTERSECTION) || !Peek("&")))
            {
                e = MakeConcatenation(e, ParseConcatExp());
            }
            return e;
        }

        internal RegExp ParseRepeatExp()
        {
            RegExp e = ParseComplExp();
            while (Peek("?*+{"))
            {
                if (Match('?'))
                {
                    e = MakeOptional(e);
                }
                else if (Match('*'))
                {
                    e = MakeRepeat(e);
                }
                else if (Match('+'))
                {
                    e = MakeRepeat(e, 1);
                }
                else if (Match('{'))
                {
                    int start = pos;
                    while (Peek("0123456789"))
                    {
                        Next();
                    }
                    if (start == pos)
                    {
                        throw new System.ArgumentException("integer expected at position " + pos);
                    }
                    int n = Convert.ToInt32(b.Substring(start, pos - start));
                    int m = -1;
                    if (Match(','))
                    {
                        start = pos;
                        while (Peek("0123456789"))
                        {
                            Next();
                        }
                        if (start != pos)
                        {
                            m = Convert.ToInt32(b.Substring(start, pos - start));
                        }
                    }
                    else
                    {
                        m = n;
                    }
                    if (!Match('}'))
                    {
                        throw new System.ArgumentException("expected '}' at position " + pos);
                    }
                    if (m == -1)
                    {
                        e = MakeRepeat(e, n);
                    }
                    else
                    {
                        e = MakeRepeat(e, n, m);
                    }
                }
            }
            return e;
        }

        internal RegExp ParseComplExp()
        {
            if (Check(RegExpSyntax.COMPLEMENT) && Match('~'))
            {
                return MakeComplement(ParseComplExp());
            }
            else
            {
                return ParseCharClassExp();
            }
        }

        internal RegExp ParseCharClassExp()
        {
            if (Match('['))
            {
                bool negate = false;
                if (Match('^'))
                {
                    negate = true;
                }
                RegExp e = ParseCharClasses();
                if (negate)
                {
                    e = MakeIntersection(MakeAnyChar(), MakeComplement(e));
                }
                if (!Match(']'))
                {
                    throw new System.ArgumentException("expected ']' at position " + pos);
                }
                return e;
            }
            else
            {
                return ParseSimpleExp();
            }
        }

        internal RegExp ParseCharClasses()
        {
            RegExp e = ParseCharClass();
            while (More() && !Peek("]"))
            {
                e = MakeUnion(e, ParseCharClass());
            }
            return e;
        }

        internal RegExp ParseCharClass()
        {
            int c = ParseCharExp();
            if (Match('-'))
            {
                return MakeCharRange(c, ParseCharExp());
            }
            else
            {
                return MakeChar(c);
            }
        }

        internal RegExp ParseSimpleExp()
        {
            if (Match('.'))
            {
                return MakeAnyChar();
            }
            else if (Check(RegExpSyntax.EMPTY) && Match('#'))
            {
                return MakeEmpty();
            }
            else if (Check(RegExpSyntax.ANYSTRING) && Match('@'))
            {
                return MakeAnyString();
            }
            else if (Match('"'))
            {
                int start = pos;
                while (More() && !Peek("\""))
                {
                    Next();
                }
                if (!Match('"'))
                {
                    throw new System.ArgumentException("expected '\"' at position " + pos);
                }
                return MakeString(b.Substring(start, pos - 1 - start));
            }
            else if (Match('('))
            {
                if (Match(')'))
                {
                    return MakeString("");
                }
                RegExp e = ParseUnionExp();
                if (!Match(')'))
                {
                    throw new System.ArgumentException("expected ')' at position " + pos);
                }
                return e;
            }
            else if ((Check(RegExpSyntax.AUTOMATON) || Check(RegExpSyntax.INTERVAL)) && Match('<'))
            {
                int start = pos;
                while (More() && !Peek(">"))
                {
                    Next();
                }
                if (!Match('>'))
                {
                    throw new System.ArgumentException("expected '>' at position " + pos);
                }
                string s = b.Substring(start, pos - 1 - start);
                int i = s.IndexOf('-');
                if (i == -1)
                {
                    if (!Check(RegExpSyntax.AUTOMATON))
                    {
                        throw new System.ArgumentException("interval syntax error at position " + (pos - 1));
                    }
                    return MakeAutomaton(s);
                }
                else
                {
                    if (!Check(RegExpSyntax.INTERVAL))
                    {
                        throw new System.ArgumentException("illegal identifier at position " + (pos - 1));
                    }
                    try
                    {
                        if (i == 0 || i == s.Length - 1 || i != s.LastIndexOf('-'))
                        {
                            throw new System.FormatException();
                        }
                        string smin = s.Substring(0, i);
                        string smax = s.Substring(i + 1, s.Length - (i + 1));
                        int imin = Convert.ToInt32(smin);
                        int imax = Convert.ToInt32(smax);
                        int digits;
                        if (smin.Length == smax.Length)
                        {
                            digits = smin.Length;
                        }
                        else
                        {
                            digits = 0;
                        }
                        if (imin > imax)
                        {
                            int t = imin;
                            imin = imax;
                            imax = t;
                        }
                        return MakeInterval(imin, imax, digits);
                    }
#pragma warning disable 168
                    catch (System.FormatException e)
#pragma warning restore 168
                    {
                        throw new System.ArgumentException("interval syntax error at position " + (pos - 1), e);
                    }
                }
            }
            else
            {
                return MakeChar(ParseCharExp());
            }
        }

        internal int ParseCharExp()
        {
            Match('\\');
            return Next();
        }
    }
}