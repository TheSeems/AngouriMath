﻿/* 
 * Copyright (c) 2019-2021 Angouri.
 * AngouriMath is licensed under MIT. 
 * Details: https://github.com/asc-community/AngouriMath/blob/master/LICENSE.md.
 * Website: https://am.angouri.org.
 */
using AngouriMath.Functions.Algebra;

namespace AngouriMath
{
    partial record Entity
    {
        public partial record Derivativef
        {
            /// <inheritdoc/>
            protected override Entity InnerEval() =>
                ExpandOnTwoAndTArguments(Expression.Evaled, Var.Evaled, Iterations,
                    (a, b, c) => (a, b, c) switch
                    {
                        (var expr, _, 0) => expr,
                        // TODO: consider Integral for negative cases
                        // TODO: should we call InnerSimlified here?
                        (var expr, Variable var, var asInt) => expr.Derive(var, asInt),
                        _ => null
                    },
                    (@this, a, b, _) => ((Derivativef)@this).New(a, b)
                    );

            /// <inheritdoc/>
            protected override Entity InnerSimplify() =>
                Var is Variable var
                ? Iterations == 0
                    ? Expression
                    : Expression.Derive(var, Iterations)
                : this;
        }
        public partial record Integralf
        {
            private Entity SequentialIntegrating(Entity expr, Variable var, int iterations)
            {
                if (iterations < 0)
                    return this;
                var changed = expr;
                for (int i = 0; i < iterations; i++)
                    changed = Integration.ComputeIndefiniteIntegral(changed, var);
                return changed;
            }

            /// <inheritdoc/>
            protected override Entity InnerEval() =>
                ExpandOnTwoAndTArguments(Expression.Evaled, Var.Evaled, Iterations,
                    (a, b, c) => (a, b, c) switch
                    {
                        (var expr, _, 0) => expr,
                        // TODO: consider Derivative for negative cases
                        (var expr, Variable var, int asInt) => SequentialIntegrating(expr, var, asInt),
                        _ => null
                    },
                    (@this, a, b, _) => ((Integralf)@this).New(a, b)
                    );

            /// <inheritdoc/>
            protected override Entity InnerSimplify() =>
               Var is Variable var ?

                ExpandOnTwoAndTArguments(Expression.InnerSimplified, Var, Iterations,
                    (a, b, c) => (a, b, c) switch
                    {
                        (var expr, _, 0) => expr,
                        // TODO: consider Derivative for negative cases
                        // TODO: should we apply InnerSimplified?
                        (var expr, Variable var, int asInt) => SequentialIntegrating(expr, var, asInt),
                        _ => null
                    },
                    (@this, a, b, _) => ((Integralf)@this).New(a, b)
                    )

                : this;
        }


        // TODO: rewrite this part too
        public partial record Limitf
        {
            /// <inheritdoc/>
            protected override Entity InnerEval() => ExpandOnTwoAndTArguments(
                Expression.Evaled, Destination.Evaled, (v: Var, ap: ApproachFrom),
                (expr, dest, vap) => vap.ap switch
                {
                    _ => null
                },
                (@this, expr, dest, vap) => ((Limitf)@this).New(expr, vap.v, dest, vap.ap)
                );

            /// <inheritdoc/>
            protected override Entity InnerSimplify() =>
                Var switch
                {
                    // if it cannot compute it, it will anyway return the node
                    Variable x => Expression.InnerSimplified.Limit(x, Destination.InnerSimplified, ApproachFrom),
                    var x => new Limitf(Expression.InnerSimplified, x, Destination.InnerSimplified, ApproachFrom)
                };

        }
    }
}
