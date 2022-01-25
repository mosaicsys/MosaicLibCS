//-------------------------------------------------------------------
/*! @file Motion.cs
 *  @brief 
 * 
 * Copyright (c) Mosaic Systems Inc.
 * Copyright (c) 2014 Mosaic Systems Inc.
 * All rights reserved.
 * 
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 * 
 *      http://www.apache.org/licenses/LICENSE-2.0
 * 
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */

using System;

using MosaicLib.Utils;
using MosaicLib.Time;

namespace MosaicLib.PartsLib.Common.Motion
{
    /// <summary>
    /// Defines optional Min and Max double values to be used in defining optional range testing, such as for an Axis.  
    /// Choice to use struct to cause coping to be the default behavior
    /// </summary>
    public struct PositionRange
    {
        public double? Min { get; set; }
        public double? Max { get; set; }

        public override string ToString()
        {
            if (Min.HasValue && Max.HasValue)
                return Fcns.CheckedFormat("[{0}, {1}]", Min, Max);
            else if (Min.HasValue)
                return Fcns.CheckedFormat("[{0}, inf)", Min);
            else if (Max.HasValue)
                return Fcns.CheckedFormat("(-Inf, {0}]", Max);
            else
                return "(-Inf, Inf)";
        }

        /// <summary>
        /// Returns true if either the Min or the Max range endpoint values have been assigned a non-null value.
        /// </summary>
        public bool IsDefined { get { return (Min.HasValue || Max.HasValue); } }
    }

    /// <summary>
    /// Defines the specificiation characteristics of a single physical Axis.
    /// </summary>
    public struct AxisSpec
    {
        public String Name { get; set; }
        public String TimeUnits { get; set; }
        public String PositionUnits { get; set; }
        public String ForceUnits { get; set; }
        public String MassUnits { get; set; }

        public double EffectiveMass { get; set; }

        public PositionRange PositionRangeHardstop { get; set; }
        public PositionRange PositionRangeLimitSwitches { get; set; }
        public PositionRange PositionRangeHomeSwitchActive { get; set; }
        public PositionRange OneRevolution { get; set; }

        public bool InfiniteRotation { get; set; }
    }

    /// <summary>
    /// This struct represents the instantanious target and trajectory information at a single point in time.  
    /// Includes the TimeStamp of the time, the target Position, Velocity and Acceleration and an axis specific (feed forward) Output value.
    /// </summary>
    public struct AxisTarget: IEquatable<AxisTarget>
    {
        public AxisTarget(AxisTarget other)
            : this()
        {
            TimeStamp = other.TimeStamp;
            TimeSpanInTrajectory = other.TimeSpanInTrajectory;
            Position = other.Position;
            Velocity = other.Velocity;
            Acceleration = other.Acceleration;
            Output = other.Output;
        }

        public QpcTimeStamp TimeStamp { get; set; }
        public TimeSpan TimeSpanInTrajectory { get; set; }  // or zero
        public double Position { get; set; }
        public double Velocity { get; set; }
        public double Acceleration { get; set; }
        public double Output { get; set; }

        public bool IsStopped { get { return (Velocity == 0.0 && Acceleration == 0.0); } }

        public bool Equals(AxisTarget other)
        {
            return (TimeStamp == other.TimeStamp
                    && TimeSpanInTrajectory == other.TimeSpanInTrajectory
                    && Position == other.Position
                    && Velocity == other.Velocity
                    && Acceleration == other.Acceleration
                    && Output == other.Output
                    );
        }

        public override string ToString()
        {
            if (!IsStopped)
                return Fcns.CheckedFormat("Time:{0:f6} Pos:{1} Vel:{2} Accel:{3} Out:{4}", TimeSpanInTrajectory.TotalSeconds, Position, Velocity, Acceleration, Output);
            else
                return Fcns.CheckedFormat("Time:{0:f6} Pos:{1} Stopped Out:{2}", TimeSpanInTrajectory.TotalSeconds, Position, Output);
        }
    }

    /// <summary>
    /// This struct gives a small set of common measurements from an axis: namely a TimeStamp, a Position and a Velocity.  Position and Velocity are
    /// referenced to the units defined in the corresponding AxisSpec object.
    /// </summary>
    public struct AxisMeasurements
    {
        public QpcTimeStamp TimeStamp { get; set; }
        public double TimeStampInSeconds { get { return TimeStamp.Time; } set { TimeStamp = new QpcTimeStamp(value); } }
        public double Position { get; set; }
        public double Velocity { get; set; }
    }

    /// <summary>
    /// Common interface shared by all trajectory generation object types.
    /// </summary>
    public interface ITrajectory
    {
        /// <summary>
        /// Evaluates this trajectory to produce an AxisTarget for the given timestamp value.
        /// </summary>
        AxisTarget GetAxisTarget(QpcTimeStamp timeStamp);

        /// <summary>
        /// Evaluates this trajectory to produce an AxisTarget for the given timeSpan from the beginning of the trajectory.
        /// </summary>
        AxisTarget GetAxisTarget(TimeSpan timeSpan);

        /// <summary>
        /// Returns true if the given timeStamp is in the last phase of the trajectory (the part that can be extended indefinitely).
        /// Depending on the trajectory type and details, this extend state may produce AxisTarget values that are IsStopped true or IsStopped false.
        /// </summary>
        bool IsComplete(QpcTimeStamp timeStamp);

        /// <summary>
        /// Returns true if the given timeSpan is in the last phase of the trajectory (the part that can be extended indefinitely).
        /// Depending on the trajectory type and details, this extend state may produce AxisTarget values that are IsStopped true or IsStopped false.
        /// </summary>
        bool IsComplete(TimeSpan timeSpan);
    }

    /// <summary>
    /// Base class for most derived Trajectory types.  Implements prior variants of GetAxisTarget and provides default implementations for IsComplete.
    /// </summary>
    public abstract class TrajectoryBase : ITrajectory
    {
        public QpcTimeStamp FirstPhaseStartTimeStamp { get; protected set; }
        public TimeSpan TrajectoryRunTimeSpan { get; protected set; }
        public bool TrajectoryEndIsTimeBased { get; protected set; }

        #region ITrajectory Members

        public AxisTarget GetAxisTarget(QpcTimeStamp timeStamp)
        {
            return GetAxisTarget(timeStamp - FirstPhaseStartTimeStamp);
        }

        public abstract AxisTarget GetAxisTarget(TimeSpan timeSpan);

        public bool IsComplete(QpcTimeStamp timeStamp)
        {
            return IsComplete(timeStamp - FirstPhaseStartTimeStamp);
        }

        public virtual bool IsComplete(TimeSpan timeSpan)
        {
            if (TrajectoryEndIsTimeBased)
                return (timeSpan >= TrajectoryRunTimeSpan);
            else
                return false;
        }

        #endregion
    }

    /// <summary>
    /// A Null Trajectory is one that always generates AxisTargets that contain zeros.
    /// </summary>
    public class NullTrajectory : TrajectoryBase
    {
        public NullTrajectory(bool setStartTimeStamp = true)
        {
            FirstPhaseStartTimeStamp = setStartTimeStamp ? QpcTimeStamp.Now : QpcTimeStamp.Zero;
            TrajectoryRunTimeSpan = TimeSpan.Zero;
            TrajectoryEndIsTimeBased = false;   // trajectory never ends
        }

        public override AxisTarget GetAxisTarget(TimeSpan timeSpan)
        {
            return new AxisTarget() { TimeStamp = FirstPhaseStartTimeStamp + timeSpan, TimeSpanInTrajectory = timeSpan };      // everything else is zero
        }
    }

    /// <summary>
    /// A Constant Output Trajectory is one that always generates AxisTargets with zero's for position and velocity and with a constant value for the Output.
    /// </summary>
    public class ConstantOutputTrajectory : TrajectoryBase
    {
        public ConstantOutputTrajectory(bool setStartTimeStamp = true)
        {
            FirstPhaseStartTimeStamp = setStartTimeStamp ? QpcTimeStamp.Now : QpcTimeStamp.Zero;
            TrajectoryRunTimeSpan = TimeSpan.Zero;
            TrajectoryEndIsTimeBased = false;   // trajectory never ends
        }

        public double Output { get; set; }

        public override AxisTarget GetAxisTarget(TimeSpan timeSpan)
        {
            return new AxisTarget() { Output = Output, TimeStamp = FirstPhaseStartTimeStamp + timeSpan, TimeSpanInTrajectory = timeSpan };      // everything else is zero
        }
    }

    /// <summary>
    /// A Stopped trajectory is one that generates AxisTargets at a fixed position with a fixed output value.
    /// </summary>
    public class StoppedTrajectory : TrajectoryBase
    {
        public StoppedTrajectory(bool setStartTimeStamp = true)
        {
            FirstPhaseStartTimeStamp = setStartTimeStamp ? QpcTimeStamp.Now : QpcTimeStamp.Zero;
            TrajectoryRunTimeSpan = TimeSpan.Zero;
            TrajectoryEndIsTimeBased = true;   // trajectory has always ended
        }

        public double Output { get; set; }
        public double Position { get; set; }

        public override AxisTarget GetAxisTarget(TimeSpan timeSpan)
        {
            return new AxisTarget() { Position = Position, Output = Output, Velocity = 0.0, Acceleration = 0.0, TimeStamp = FirstPhaseStartTimeStamp + timeSpan, TimeSpanInTrajectory = timeSpan };      // everything else is zero
        }
    }

    /// <summary>
    /// Struct contains the paremeter values that are used with specific trajectory types to define its actual path.
    /// These include Velocity, Acceleration, Deceleration and some feedforward terms
    /// </summary>
    public struct TrajectorySettings
    {
        /// <summary>Copy constructor</summary>
        public TrajectorySettings(TrajectorySettings rhs) 
            : this ()
        {
            ConstantOutput = rhs.ConstantOutput;
            KVelocityOutput = rhs.KVelocityOutput;
            KAccelerationOutput = rhs.KAccelerationOutput;
            Acceleration = rhs.Acceleration;
            Deceleration = rhs.Deceleration;
            Velocity = rhs.Velocity;
        }

        public double ConstantOutput { get; set; }
        public double KVelocityOutput { get; set; }
        public double KAccelerationOutput { get; set; }

        public double Acceleration { get; set; }
        public double Deceleration { get; set; }
        public double AccelAndDecel { set { Acceleration = Deceleration = value; } }

        public double Velocity { get; set; }

        public void UpdateTargetOutput(ref AxisTarget target)
        {
            target.Output += ConstantOutput + KVelocityOutput * target.Velocity + KAccelerationOutput * target.Acceleration;
        }

        public void AdvanceTargetTimestamp(ref AxisTarget target, TimeSpan advanceTimeSpan)
        {
            double advancePeriodInSeconds = advanceTimeSpan.TotalSeconds;

            target.TimeSpanInTrajectory += advanceTimeSpan;
            target.TimeStamp += advancePeriodInSeconds;
            target.Position += (target.Velocity * advancePeriodInSeconds);
            if (target.Acceleration != 0)
            {
                target.Velocity += target.Acceleration * advancePeriodInSeconds;
                target.Position += 0.5 * target.Acceleration * advancePeriodInSeconds * advancePeriodInSeconds;
            }

            UpdateTargetOutput(ref target);
        }
    }

    /// <summary>
    /// A Jog Trajectory is one that slews the velocity from the initial velocity to the target velocity and then holds it there indefinately.
    /// The target position will continue to advance at the current velocity indefinitely.
    /// </summary>
    public class JogTrajectory : TrajectoryBase
    {
        public AxisMeasurements InitialMeasurements { get; set; }
        public TrajectorySettings TrajectorySettings { get; set; }

        public JogTrajectory Setup()
        {
            TrajectoryEndIsTimeBased = false;   // trajectory never ends

            double initialVelocity = InitialMeasurements.Velocity;
            double velocityDelta = TrajectorySettings.Velocity - initialVelocity;
            double acceleration = TrajectorySettings.Acceleration * ((velocityDelta > 0) ? 1.0 : -1.0);

            phase1_AccelOrDecel = new AxisTarget() 
            {
                TimeSpanInTrajectory = TimeSpan.Zero,
                TimeStamp = InitialMeasurements.TimeStamp, 
                Position = InitialMeasurements.Position, 
                Velocity = InitialMeasurements.Velocity, 
                Acceleration = acceleration
            };

            double accelDecelPhaseDuration = velocityDelta / acceleration;
            double accelDecelPhaseDeltaPosition = initialVelocity * accelDecelPhaseDuration + 0.5 * acceleration * (accelDecelPhaseDuration * accelDecelPhaseDuration);
            TimeSpan accelDecelPhaseDurationTimeSpan = TimeSpan.FromSeconds(accelDecelPhaseDuration);

            phase2_cruise = new AxisTarget()
            {
                TimeSpanInTrajectory = accelDecelPhaseDurationTimeSpan,
                TimeStamp = InitialMeasurements.TimeStamp + accelDecelPhaseDurationTimeSpan,
                Position = InitialMeasurements.Position + accelDecelPhaseDeltaPosition,
                Velocity = TrajectorySettings.Velocity,
                Acceleration = 0.0
            };

            FirstPhaseStartTimeStamp = InitialMeasurements.TimeStamp;
            TrajectoryRunTimeSpan = accelDecelPhaseDurationTimeSpan;

            return this;
        }

        private AxisTarget phase1_AccelOrDecel;
        private AxisTarget phase2_cruise;

        public override AxisTarget GetAxisTarget(TimeSpan timeSpan)
        {
            AxisTarget phaseStartTarget;
            
            if (timeSpan < phase2_cruise.TimeSpanInTrajectory)
            {
                phaseStartTarget = phase1_AccelOrDecel;
            }
            else
            {
                phaseStartTarget = phase2_cruise;
                timeSpan -= phase2_cruise.TimeSpanInTrajectory;
            }

            AxisTarget target = phaseStartTarget;
            TrajectorySettings.AdvanceTargetTimestamp(ref target, timeSpan);

            return target;
        }
    }

    /// <summary>
    /// A Point to Point trajectory implements the traditional trapizoidal trajectory generation to move from being stopped at one point to being stopped at another
    /// point and to use the time necessary to do the move without exceeding the stated acceleration, deceleration and velocity limits.
    /// </summary>
    public class PointToPointTrajectory : TrajectoryBase
    {
        public PointToPointTrajectory()
        {
            TrajectoryEndIsTimeBased = true;
        }

        public AxisMeasurements InitialMeasurements { get; set; }
        public AxisMeasurements FinalMeasurements { get; set; }
        public TrajectorySettings TrajectorySettings { get; set; }

        public PointToPointTrajectory Setup()
        {
            double totalPositionDelta = FinalMeasurements.Position - InitialMeasurements.Position;
            double totalDistance = Math.Abs(totalPositionDelta);

            // first determine if this trajectory is a velocity triangle (no cruise period) or a trapazoid (has cruise period).

            double accel = TrajectorySettings.Acceleration;
            double decel = TrajectorySettings.Deceleration;
            double accelTime = TrajectorySettings.Velocity / accel;
            double decelTime = TrajectorySettings.Velocity / decel;
            double cruiseTime = 0.0;
            double cruiseVelocity = TrajectorySettings.Velocity;

            double accelDistance = (0.5 * accel * accelTime * accelTime);
            double decelDistance = (0.5 * decel * decelTime * decelTime);

            double cruiseDistance = (totalDistance - (accelDistance + decelDistance));

            if (cruiseDistance < 0.0)
            {
                // accel and decel distances are already to large - need to recalculate shorter accel and decel times.
                double accelOverDecel = accel/decel;

                accelTime = Math.Sqrt(totalDistance / (0.5 * accel * (1.0 + accelOverDecel)));
                decelTime = accelOverDecel * accelTime;

                accelDistance = (0.5 * accel * accelTime * accelTime);
                decelDistance = (0.5 * decel * decelTime * decelTime);
                cruiseDistance = 0.0;

                cruiseVelocity = Math.Min(accel * accelTime, decel * decelTime);
            }
            else
            {
                // have cruise period
                cruiseTime = (cruiseDistance / cruiseVelocity); 
            }

            // assign signs to the values
            if (totalPositionDelta >= 0)
            {
                decel = -decel;
            }
            else
            {
                accel = -accel;
                cruiseVelocity = -cruiseVelocity;
                accelDistance = -accelDistance;
                cruiseDistance = -cruiseDistance;
                decelDistance = -decelDistance;
            }

            // phases: accel, cruise, decel, stopped.
            TimeSpan phase1StartTimeSpan = TimeSpan.Zero;
            TimeSpan phase2StartTimeSpan = phase1StartTimeSpan + TimeSpan.FromSeconds(accelTime);
            TimeSpan phase3StartTimeSpan = phase2StartTimeSpan + TimeSpan.FromSeconds(cruiseTime);
            TimeSpan phase4StartTimeSpan = phase3StartTimeSpan + TimeSpan.FromSeconds(decelTime);

            phase1_Accel = new AxisTarget() { TimeSpanInTrajectory = phase1StartTimeSpan, TimeStamp = InitialMeasurements.TimeStamp + phase1StartTimeSpan, Position = InitialMeasurements.Position, Velocity = 0.0, Acceleration = accel };
            phase2_Cruise = new AxisTarget() { TimeSpanInTrajectory = phase2StartTimeSpan, TimeStamp = InitialMeasurements.TimeStamp + phase2StartTimeSpan, Position = InitialMeasurements.Position + accelDistance, Velocity = cruiseVelocity, Acceleration = 0.0 };
            phase3_Decel = new AxisTarget() { TimeSpanInTrajectory = phase3StartTimeSpan, TimeStamp = InitialMeasurements.TimeStamp + phase3StartTimeSpan, Position = InitialMeasurements.Position + accelDistance + cruiseDistance, Velocity = cruiseVelocity, Acceleration = decel };
            phase4_EndHold = new AxisTarget() { TimeSpanInTrajectory = phase4StartTimeSpan, TimeStamp = InitialMeasurements.TimeStamp + phase4StartTimeSpan, Position = FinalMeasurements.Position, Velocity = 0.0, Acceleration = 0.0 };

            FirstPhaseStartTimeStamp = InitialMeasurements.TimeStamp;
            TrajectoryRunTimeSpan = phase4_EndHold.TimeSpanInTrajectory;

            return this;
        }

        // phases: accel, cruise, decel, stopped.
        private AxisTarget phase1_Accel, phase2_Cruise, phase3_Decel, phase4_EndHold;

        public override AxisTarget GetAxisTarget(TimeSpan timeSpan)
        {
            AxisTarget phaseStartTarget;

            if (timeSpan >= phase4_EndHold.TimeSpanInTrajectory)
                phaseStartTarget = phase4_EndHold;
            else if (timeSpan >= phase3_Decel.TimeSpanInTrajectory)
                phaseStartTarget = phase3_Decel;
            else if (timeSpan >= phase2_Cruise.TimeSpanInTrajectory)
                phaseStartTarget = phase2_Cruise;
            else
                phaseStartTarget = phase1_Accel;

            TimeSpan phaseTimeSpan = timeSpan - phaseStartTarget.TimeSpanInTrajectory;

            AxisTarget adjustedTarget = phaseStartTarget;
            TrajectorySettings.AdvanceTargetTimestamp(ref adjustedTarget, phaseTimeSpan);

            return adjustedTarget;
        }
    }

    /// <summary>
    /// class gives state of an axis including its active Trajectory, its most recent Target and its most recent Measurements
    /// </summary>
    public class AxisState
    {
        public TrajectorySettings TrajectorySettings { get; set; }
        public ITrajectory Trajectory { get; set; }
        public AxisTarget Target { get; set; }

        public AxisMeasurements Measurements { get; set; }

        public bool TrajectoryIsComplete { get { return Trajectory.IsComplete(Target.TimeStamp); } }
        public bool InMotion { get { return (!Target.IsStopped || !TrajectoryIsComplete); } }
    }
}

//-------------------------------------------------------------------
