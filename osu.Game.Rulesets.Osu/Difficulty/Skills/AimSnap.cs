﻿// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Game.Rulesets.Difficulty.Preprocessing;
using osu.Game.Rulesets.Osu.Difficulty.Preprocessing;
using osu.Game.Rulesets.Osu.Objects;
using osuTK;

namespace osu.Game.Rulesets.Osu.Difficulty.Skills
{
    /// <summary>
    /// Represents the skill required to correctly aim at every object in the map with a uniform CircleSize and normalized distances.
    /// </summary>
    public class AimSnap : OsuSkill
    {
        private double StrainDecay = 0.2;
        private const float prevMultiplier = 0.45f;
        private double degree30 = Math.PI / 3.0;

        protected override double SkillMultiplier => 2500;
        protected override double StrainDecayBase => StrainDecay;
        protected override double StarMultiplierPerRepeat => 1.05;

        protected override double StrainValueOf(DifficultyHitObject current)
        {
            StrainDecay = .2;

            if (current.BaseObject is Spinner)
                return 0;

            double strain = 0;

            if (Previous.Count > 1)
            {
                // though we are on current, we want to use current as reference for previous,
                // so for what its worth, difficulty calculation is a function of Previous[0]
                var osuPrevObj = (OsuDifficultyHitObject)Previous[1];
                var osuCurrentObj = (OsuDifficultyHitObject)Previous[0];
                var osuNextObj = (OsuDifficultyHitObject)current;

                // Here we set a custom strain decay rate that decays based on # of objects rather than MS.
                // This is just so we can focus on balancing only the strain rewarded, and time no longer matters.
                // this will make a repeated pattern "cap out" or reach 85 maximum difficulty in 12 objects.
                StrainDecay = Math.Pow(.85, 1000.0 / Math.Min(osuCurrentObj.StrainTime, 500.0));

                // here we generate a value of being snappy or flowy that is fed into the gauss error function to build a probability.
                var x = (osuCurrentObj.JumpDistance - (Math.Pow(Math.Sin(Math.Min(osuNextObj.JumpDistance, Math.PI / 2)), 2)
                        * (.5 * osuCurrentObj.JumpDistance)
                        * Math.Pow(Math.Sin((double)osuCurrentObj.Angle / 2), 2)))
                        * (osuCurrentObj.DeltaTime - 50);

                var distributionMean = Math.Max(65, 65 + 2 * (32 - osuCurrentObj.BaseObject.Radius));

                // this is where we use an ERF function to derive a probability.
                var snappiness = 0.5 * erf((-distributionMean + x) / (25 * Math.Sqrt(2))) + 0.5;

                // Create velocity vectors, scale prior by prevMultiplier
                var prevVector = Vector2.Multiply(Vector2.Divide(osuPrevObj.DistanceVector, (float)osuPrevObj.StrainTime), prevMultiplier);
                var currVector = Vector2.Divide(osuCurrentObj.DistanceVector, (float)osuCurrentObj.StrainTime);

                double sliderVelocity = 0;
                if (osuPrevObj.BaseObject is Slider osuSlider)
                  sliderVelocity = (Math.Max(osuSlider.LazyTravelDistance, 1) / 50) / (50 + osuSlider.LazyTravelTime);

                double adjVelocity = 0;

                // add them to get our final velocity, length is the observed velocity and thus the difficulty.
                if (osuCurrentObj.Angle < degree30)
                  adjVelocity = Math.Abs(currVector.Length - prevVector.Length);
                else
                {
                  var prevVectorRotPos = new Vector2(prevVector.X * (float)Math.Cos(degree30) + prevVector.Y * (float)Math.Cos(degree30),
                                             prevVector.X * (float)Math.Sin(degree30) + prevVector.Y * (float)Math.Sin(degree30));
                  var prevVectorRotNeg = new Vector2(prevVector.X * (float)Math.Cos(0 - degree30) + prevVector.Y * (float)Math.Cos(0 - degree30),
                                             prevVector.X * (float)Math.Sin(0 - degree30) + prevVector.Y * (float)Math.Sin(0 - degree30));
                  adjVelocity = Math.Min(currVector.Length, Math.Min(Vector2.Add(currVector, prevVectorRotPos).Length, Vector2.Add(currVector,prevVectorRotNeg).Length));
                }

                adjVelocity = adjVelocity / ((osuCurrentObj.StrainTime - 40) / osuCurrentObj.StrainTime);

                strain = (adjVelocity
                        + sliderVelocity
                        + Math.Sqrt(adjVelocity * sliderVelocity))
                          * snappiness;
            }

            return strain;

        }

        private static double erf(double x)
        {
            // constants
            double a1 = 0.254829592;
            double a2 = -0.284496736;
            double a3 = 1.421413741;
            double a4 = -1.453152027;
            double a5 = 1.061405429;
            double p = 0.3275911;

            // Save the sign of x
            int sign = 1;
            if (x < 0)
                sign = -1;
            x = Math.Abs(x);

            // A&S formula 7.1.26
            double t = 1.0 / (1.0 + p * x);
            double y = 1.0 - (((((a5 * t + a4) * t) + a3) * t + a2) * t + a1) * t * Math.Exp(-x * x);

            return sign * y;
        }
    }
}
