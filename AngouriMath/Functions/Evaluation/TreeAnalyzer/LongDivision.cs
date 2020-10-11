/*
 * Copyright (c) 2019-2020 Angourisoft
 * 
 * Permission is hereby granted, free of charge, to any person obtaining a copy of this software and associated documentation files (the "Software"), to deal in the Software without restriction, including without limitation the rights to use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies of the Software, and to permit persons to whom the Software is furnished to do so, subject to the following conditions:
 * 
 * The above copyright notice and this permission notice shall be included in all copies or substantial portions of the Software.
 * 
 * THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
 */
using System.Collections.Generic;
using System.Linq;
using PeterO.Numbers;

namespace AngouriMath.Functions
{
    using static Entity;
    using static Entity.Number;
    internal static partial class TreeAnalyzer
    {
        internal sealed class PolynomialInformation
        {
            private readonly Dictionary<Variable, Dictionary<EDecimal, Entity>> monoInfo = new();
            private readonly Dictionary<Entity, Variable> replacements = new();
            private readonly Dictionary<Variable, Entity> revertReplacements = new();
            public IReadOnlyDictionary<Variable, Dictionary<EDecimal, Entity>> MonoInfo => monoInfo;
            public IReadOnlyDictionary<Entity, Variable> Replacements => replacements;
            public IReadOnlyDictionary<Variable, Entity> RevertReplacements => revertReplacements;
            public void AddReplacement(IEnumerable<Variable> existingVars, Entity value)
            {
                var variable = Variable.CreateTemp(existingVars.Concat(revertReplacements.Keys));
                replacements[value] = variable;
                revertReplacements[variable] = value;
            }
            public void AddMonoInfo(Variable variable, Dictionary<EDecimal, Entity>? powers)
            {
                if (powers is { }) monoInfo.Add(variable, powers);
            }
        }

        internal static PolynomialInformation GatherAllPossiblePolynomials(Entity expr, bool replaceVars)
        {
            // Init
            var res = new PolynomialInformation();

            if (replaceVars)
            {
                // Replace all variables we can
                foreach (var varMentioned in expr.Vars)
                    res.AddReplacement(expr.Vars, GetMinimumSubtree(expr, varMentioned));
                expr = expr.Substitute(res.Replacements);
            }

            // Gather info about each var as if this var was the only argument of the polynomial P(x)
            var children = Sumf.LinearChildren(expr);
            foreach (var varMentioned in expr.Vars)
                res.AddMonoInfo(varMentioned, Algebra.AnalyticalSolving.PolynomialSolver.GatherMonomialInformation
                   <EDecimal, PrimitiveDecimal>(children, varMentioned));
            return res;
        }

        /// <summary>
        /// Divides one polynomial over another one:
        /// <a href="https://en.wikipedia.org/wiki/Polynomial_long_division"/>
        /// </summary>
        internal static (Entity Divided, Entity Remainder)? PolynomialLongDivision(Entity p, Entity q)
        {
            if (!p.Vars.Any() || !q.Vars.Any())
                return null; // There are no variables to find polynomial as

            // ---> (x^0.6 + 2x^0.3 + 1) / (x^0.3 + 1)
            var replacementInfo = GatherAllPossiblePolynomials(p + q, replaceVars: true);

            var originalP = p;
            var originalQ = q;

            // TODO: remove extra call to GatherAllPossiblePolynomials above with p + q
            // by making GatherAllPossiblePolynomials accept multiple polynomials
            p = p.Substitute(replacementInfo.Replacements);
            q = q.Substitute(replacementInfo.Replacements);

            var monoinfoP = GatherAllPossiblePolynomials(p.Expand(), replaceVars: false).MonoInfo;
            var monoinfoQ = GatherAllPossiblePolynomials(q.Expand(), replaceVars: false).MonoInfo;

            // First attempt to find polynoms
            var polyvar = monoinfoP.Keys.FirstOrDefault(monoinfoQ.ContainsKey);
            // cannot divide, return unchanged
            if (polyvar is null) return null;
            var maxpowP = monoinfoP[polyvar].Keys.Max();
            var maxpowQ = monoinfoQ[polyvar].Keys.Max();
            var maxvalP = monoinfoP[polyvar][maxpowP];
            var maxvalQ = monoinfoQ[polyvar][maxpowQ];

            // TODO: add case where all powers are non-positive
            // for now just return polynomials unchanged
            if (maxpowP.LessThan(maxpowQ)) return null;

            var result = new Dictionary<EDecimal, Entity>();
            // possibly very long process
            while (maxpowP.GreaterThanOrEquals(maxpowQ))
            {
                // KeyPair is ax^n with Key=n, Value=a
                var deltapow = maxpowP - maxpowQ;
                var deltamul = maxvalP / maxvalQ;
                result[deltapow] = deltamul;

                foreach (var n in monoinfoQ[polyvar])
                {
                    // TODO: precision loss can happen here. MUST be fixed somehow
                    var newpow = deltapow + n.Key;
                    if (monoinfoP[polyvar].TryGetValue(newpow, out var existing))
                        monoinfoP[polyvar][newpow] = existing - deltamul * n.Value;
                    else
                        monoinfoP[polyvar][newpow] = -deltamul * n.Value;
                }
                _ = monoinfoP[polyvar].Remove(maxpowP);
                if (monoinfoP[polyvar].Count == 0)
                    break;

                maxpowP = monoinfoP[polyvar].Keys.Max();
                maxvalP = monoinfoP[polyvar][maxpowP];
            }

            // check if all left in P is zero. If something left, division is impossible => return P / Q
            Entity rest = 0;
            foreach (var coef in monoinfoP[polyvar])
                if (coef.Value.Simplify() is not Integer(0) and var simplified)
                    rest += simplified * MathS.Pow(polyvar, coef.Key);
            rest /= q;

            Entity res = 0;
            foreach (var pair in result)
                res += pair.Value.Simplify(5) * MathS.Pow(polyvar, pair.Key);
            return (res.Substitute(replacementInfo.RevertReplacements),
                    rest.Substitute(replacementInfo.RevertReplacements));
        }
    }
}
