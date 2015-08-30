using System;
using System.Collections.Generic;
using System.Text;

namespace Gondwana.Physics
{
    public static class Kinematics
    {
        public static float GetDistanceByAcceleration(ref ConstantAccelerationVariables vars)
        {
            vars.Distance = (vars.VelocityInit * vars.Time) + 
                ((float)0.5 * vars.Acceleration * vars.Time * vars.Time);

            return vars.Distance;
        }

        public static float GetDistanceByVelocities(ref ConstantAccelerationVariables vars)
        {
            vars.Distance = (vars.VelocityInit + vars.VelocityFinal) / (float)2 * vars.Time;

            return vars.Distance;
        }

        public static float GetFinalVelocityByTime(ref ConstantAccelerationVariables vars)
        {
            vars.VelocityFinal = vars.VelocityInit + (vars.Acceleration * vars.Time);
            
            return vars.VelocityFinal;
        }

        public static float GetFinalVelocityByDistance(ref ConstantAccelerationVariables vars)
        {
            vars.VelocityFinal = (float)Math.Sqrt((double)
                ((vars.VelocityInit * vars.VelocityInit) + 
                ((float)2 * vars.Acceleration * vars.Distance)));

            return vars.VelocityFinal;
        }
    }
}
