//-------------------------------------------------------------------
/*! @file Actuator.cs
 *  @brief Provides a simple model of a two position actuator
 * 
 * Copyright (c) Mosaic Systems Inc.
 * Copyright (c) 2011 Mosaic Systems Inc.
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
using System.Runtime.Serialization;
using System.Collections.Generic;

using MosaicLib.Time;
using MosaicLib.Utils;

namespace MosaicLib.PartsLib.Helpers
{
    /// <summary>
    /// Defines the positions that the ActuatorBase can be in/moved to
    /// <para/>Valid targets: MoveToPos1 (1), MoveToPos2 (2), Inbetween (5), None (6)
    /// <para/>Other known states: AtPos1 (1), AtPos2 (2), MovingToPos1 (3), MovingToPos2 (4), Undefined (default, 0), Unknown (7), Fault (8)
    /// </summary>
    public enum ActuatorPosition : int
    {
        /// <summary>(0)</summary>
        Undefined = 0,

        /// <summary>(1)</summary>
        AtPos1 = 1,

        /// <summary>(2)</summary>
        AtPos2 = 2,

        /// <summary>(3)</summary>
        MovingToPos1 = 3,

        /// <summary>(4)</summary>
        MovingToPos2 = 4,
        
        /// <summary>(5)</summary>
        InBetween = 5,
        
        /// <summary>(6)</summary>
        None = 6,
        
        /// <summary>(7)</summary>
        Unknown = 7,
        
        /// <summary>(8)</summary>
        Fault = 8,

        /// <summary>aka AtPos1 (1)</summary>
        MoveToPos1 = AtPos1,

        /// <summary>aka AtPos2 (2)</summary>
        MoveToPos2 = AtPos2,
    }

    public static partial class ExtensionMethods
    {
        /// <summary>Returns true if the given pos is None</summary>
        public static bool IsNone(this ActuatorPosition pos) { return (pos == ActuatorPosition.None); }

        /// <summary>Returns true if the given pos is AtPos1</summary>
        public static bool IsAtPos1(this ActuatorPosition pos)  { return (pos == ActuatorPosition.AtPos1); }

        /// <summary>Returns true if the given pos is AtPos2</summary>
        public static bool IsAtPos2(this ActuatorPosition pos) { return (pos == ActuatorPosition.AtPos2); }

        /// <summary>Returns true if the given pos is AtPos2</summary>
        public static bool IsInBetween(this ActuatorPosition pos) { return (pos == ActuatorPosition.InBetween); }
        
        /// <summary>Returns true if the given pos is MovingToPos1 or MovingToPos2</summary>
        public static bool IsInMotion(this ActuatorPosition pos) { return (pos == ActuatorPosition.MovingToPos1 || pos == ActuatorPosition.MovingToPos2); }

        /// <summary>Returns true if the given targetPos is MoveToPos1 (AtPos1), MoveToPos2 (AtPos2), or None.  May optionally permit InBetween</summary>
        public static bool IsTargetPositionValid(this ActuatorPosition targetPos, bool allowInBetween = false) { return (targetPos == ActuatorPosition.MoveToPos1|| targetPos == ActuatorPosition.MoveToPos2 || targetPos == ActuatorPosition.None || (allowInBetween && targetPos == ActuatorPosition.InBetween)); }
        
        /// <summary>Returns true if the given pos is AtPos1, AtPos2, MovingToPos1, MovingToPos2, or Inbetween</summary>
        public static bool IsValid(this ActuatorPosition pos) { return (pos.IsAtPos1() || pos.IsAtPos2() || pos.IsInBetween() || pos.IsInMotion()); }

        /// <summary>
        /// If the currentPos matches the AtPos1 then this method sets currentPos to the MovingToPos2, otherwise currentPos is not changed.
        /// </summary>
        public static ActuatorPosition StartMotionFromPos1ToPos2(this ActuatorPosition currentPos)
        {
            ActuatorPosition nextPos = (currentPos.IsAtPos1() ? ActuatorPosition.MovingToPos2 : currentPos);
            return nextPos;
        }

        /// <summary>
        /// If the currentPos matches the AtPos2 then this method sets currentPos to MovingToPos1, otherwise currentPos is not changed.
        /// </summary>
        public static ActuatorPosition StartMotionFromPos2ToPos1(this ActuatorPosition currentPos)
        {
            ActuatorPosition nextPos = (currentPos.IsAtPos2() ? ActuatorPosition.MovingToPos1 : currentPos);
            return nextPos;
        }

        /// <summary>
        /// Helper EM to generate an ActuatorPosition given atPos1 and atPos2 booleans.
        /// Returns AtPos1 (1,0), AtPos2 (0,1), InBetween (0,0) or Fault (1,1) based on the given values of atPos1 and atPos2
        /// </summary>
        public static ActuatorPosition SetFrom(this ActuatorPosition ignoredValue, bool atPos1, bool atPos2)
        {
            if (atPos1 && !atPos2)
                return ActuatorPosition.AtPos1;
            else if (!atPos1 && atPos2)
                return ActuatorPosition.AtPos2;
            else if (!atPos1 && !atPos2)
                return ActuatorPosition.InBetween;
            else
                return ActuatorPosition.Fault;
        }

        /// <summary>
        /// Method is used to help convert ActuatorPosition values into a target position in percentage
        /// </summary>
        public static double GetTargetPositionInPercent(this ActuatorPosition targetPos, bool mapInBetweenToHalfWay = false, double currentPositionInPercent = 0.0)
        {
            switch (targetPos)
            {
                case ActuatorPosition.MoveToPos1: return 0.0;
                case ActuatorPosition.MoveToPos2: return 100.0;
                case ActuatorPosition.InBetween: return (mapInBetweenToHalfWay ? 50.0 : currentPositionInPercent);
                default: return currentPositionInPercent;
            }
        }
    }

    public interface IActuatorState : IEquatable<IActuatorState>
    {
        ActuatorPosition TargetPos { get; }
        string TargetPosStr { get; }
        /// <summary>Returns the target position in percent from Pos1 (0%) to Pos2 (100%)</summary>
        double TargetPositionInPercent { get; }

        ActuatorPosition PosState { get; }
        /// <summary>Returns the position in percent from Pos1 (0%) to Pos2 (100%)</summary>
        string PosStateStr { get; }
        double PositionInPercent { get; }

        bool IsAtPos1 { get; }
        bool IsAtPos2 { get; }
        bool IsAtTarget { get; }
        bool IsTargetNone { get; }
        bool IsInMotion { get; }
        bool IsMovingToTarget { get; }
        bool IsValid { get; }
    }

    public class ActuatorConfig
    {
        public ActuatorConfig() 
        {
            InitialPos = ActuatorPosition.Unknown;
        }

        public ActuatorConfig(string name, string pos1Name, string pos2Name, TimeSpan motionTime, ActuatorPosition initialPos = ActuatorPosition.AtPos1) 
            : this(name, pos1Name, pos2Name, motionTime, motionTime, initialPos) 
        { }

        public ActuatorConfig(string name, string pos1Name, string pos2Name, TimeSpan motion1To2Time, TimeSpan motion2To1Time, ActuatorPosition initialPos = ActuatorPosition.AtPos1)
        {
            Name = name;
            Pos1Name = pos1Name;
            Pos2Name = pos2Name;
            Motion1To2Time = motion1To2Time;
            Motion2To1Time = motion2To1Time;
            InitialPos = initialPos;
        }

        public ActuatorConfig(ActuatorConfig other)
        {
            Name = other.Name;
            Pos1Name = other.Pos1Name;
            Pos2Name = other.Pos2Name;
            Motion1To2Time = other.Motion1To2Time;
            Motion2To1Time = other.Motion2To1Time;
            InitialPos = other.InitialPos;
        }

        public string Name { get; set; }
        public string Pos1Name { get; set; }
        public string Pos2Name { get; set; }
        public TimeSpan Motion1To2Time { get; set; }
        public TimeSpan Motion2To1Time { get; set; }
        public ActuatorPosition InitialPos { get; set; }

        public virtual string ToString(ActuatorPosition pos)
        {
            switch (pos)
            {
                case ActuatorPosition.AtPos1: return Pos1Name;
                case ActuatorPosition.AtPos2: return Pos2Name;
                case ActuatorPosition.MovingToPos1: return "MovingTo" + Pos1Name;
                case ActuatorPosition.MovingToPos2: return "MovingTo" + Pos2Name;
                case ActuatorPosition.Undefined:
                case ActuatorPosition.None:
                case ActuatorPosition.InBetween:
                case ActuatorPosition.Fault:
                default:
                    return pos.ToString();
            }
        }

        /// <summary>
        /// Generates and returns a default InitialActuatorState based on the ActuatorConfig contained here.
        /// </summary>
        public IActuatorState GetDefaultInitialActuatorState()
        {
            return new ActuatorState() 
            { 
                TargetPos = InitialPos, 
                TargetPosStr = InitialPos.ToString(), 
                TargetPositionInPercent = InitialPos.GetTargetPositionInPercent(mapInBetweenToHalfWay: true),
                PosState = InitialPos,
                PosStateStr = InitialPos.ToString(),
                PositionInPercent = InitialPos.GetTargetPositionInPercent(mapInBetweenToHalfWay: true),
            };
        }
    }

    public class ActuatorState : IActuatorState
    {
        public ActuatorState()
        {
            SetFrom(null);
        }

        public ActuatorState(IActuatorState other)
        {
            SetFrom(other);
        }

        public ActuatorState SetFrom(IActuatorState other)
        {
            if (other != null)
            {
                TargetPos = other.TargetPos;
                TargetPosStr = other.TargetPosStr;
                TargetPositionInPercent = other.TargetPositionInPercent;
                PosState = other.PosState;
                PosStateStr = other.PosStateStr;
                PositionInPercent = other.PositionInPercent;
            }
            else
            {
                TargetPos = ActuatorPosition.None;
                TargetPosStr = TargetPos.ToString();
                TargetPositionInPercent = 0.0;
                PosState = ActuatorPosition.Undefined;
                PosStateStr = TargetPos.ToString();
                PositionInPercent = 0.0;
            }

            return this;
        }

        public virtual ActuatorPosition TargetPos { get; set; }
        public virtual string TargetPosStr { get; set; }
        public virtual double TargetPositionInPercent { get; set; }
        public virtual ActuatorPosition PosState { get; set; }
        public virtual string PosStateStr { get; set; }
        public virtual double PositionInPercent { get; set; }

        public bool IsAtPos1 { get { return (IsAtTarget && PosState == ActuatorPosition.AtPos1); } }
        public bool IsAtPos2 { get { return (IsAtTarget && PosState == ActuatorPosition.AtPos2); } }
        public bool IsAtTarget { get { return ((IsTargetNone && !IsInMotion) || (TargetPos == PosState && TargetPositionInPercent == PositionInPercent)); } }
        public bool IsTargetNone { get { return (TargetPos == ActuatorPosition.None); } }
        public bool IsInMotion { get { return (PosState == ActuatorPosition.MovingToPos1 || PosState == ActuatorPosition.MovingToPos2); } }
        public bool IsMovingToTarget { get { return IsAtTarget || ((TargetPos == ActuatorPosition.MoveToPos1 && PosState == ActuatorPosition.MovingToPos1) || (TargetPos == ActuatorPosition.MoveToPos2 && PosState == ActuatorPosition.MovingToPos2)); } }
        public bool IsValid { get { return (TargetPos.IsTargetPositionValid(allowInBetween: true) && PosState.IsValid()); } }

        public virtual bool Equals(IActuatorState other)
        {
            return (other != null
                    && TargetPos == other.TargetPos
                    && TargetPosStr == other.TargetPosStr
                    && TargetPositionInPercent == other.TargetPositionInPercent
                    && PosState == other.PosState
                    && PosStateStr == other.PosStateStr
                    && PositionInPercent == other.PositionInPercent
                    );
        }

        public override string ToString()
        {
            if (IsAtTarget || IsTargetNone || IsMovingToTarget)
                return "{0} [{1:f0}%]".CheckedFormat(PosStateStr, PositionInPercent);
            else
                return "{0}/{1} [{2:f0]}%]".CheckedFormat(TargetPosStr, PosStateStr, PositionInPercent);
        }
    }

    public class ActuatorBase
    {
        public ActuatorBase(ActuatorConfig config, IActuatorState initialState = null) 
        {
            Config = new ActuatorConfig(config);

            UpdateMotionRates();

            privateState = new ActuatorState(initialState ?? Config.GetDefaultInitialActuatorState());

            logger = new Logging.Logger(config.Name, Logging.LogGate.All);

            if (config.InitialPos.IsTargetPositionValid(allowInBetween: true))
            {
                privateState.TargetPos = privateState.PosState = config.InitialPos;
                privateState.TargetPositionInPercent = privateState.PositionInPercent = GetTargetPositionInPercent(config.InitialPos, mapInBetweenToHalfWay: true);
                privateState.TargetPosStr = privateState.PosStateStr = config.ToString(config.InitialPos);
            }

            logger.Info.Emit("Initial state is:{0} [{1} {2:f0}%]", privateState.PosStateStr, privateState.PosState, privateState.PositionInPercent);

            PublishState();
        }

        QpcTimeStamp lastServiceTime = QpcTimeStamp.Now;

        public void Service(QpcTimeStamp now, bool forcePublish = false, bool reset = false) 
        {
            TimeSpan rawDT = now - lastServiceTime;
            if (now > lastServiceTime)
                lastServiceTime = now;

            TimeSpan dt = rawDT.Clip(TimeSpan.Zero, TimeSpan.MaxValue);

            Service(dt, forcePublish: forcePublish, reset: reset);
        }

        public virtual void Service(TimeSpan dt, bool forcePublish = false, bool reset = false)
        {
            // first update the positionInPercent

            double nextPositionInPercent = privateState.PositionInPercent;
            ActuatorPosition nextPosState = ActuatorPosition.None;

            if (reset)
            {
                nextPositionInPercent = privateState.TargetPositionInPercent;
                nextPosState = ActuatorPosition.InBetween;
            }
            else if (privateState.TargetPositionInPercent > privateState.PositionInPercent)
            {
                double dtPercent = dt.TotalSeconds * motion1To2PercentPerSecond;
                if (nextPositionInPercent + dtPercent > privateState.TargetPositionInPercent)
                {
                    nextPositionInPercent = privateState.TargetPositionInPercent;
                }
                else
                {
                    nextPositionInPercent += dtPercent;
                    nextPosState = ActuatorPosition.MovingToPos2;
                }
            }
            else if (privateState.TargetPositionInPercent < privateState.PositionInPercent)
            {
                double dtPercent = dt.TotalSeconds * motion2To1PercentPerSecond;
                if (nextPositionInPercent - dtPercent < privateState.TargetPositionInPercent)
                {
                    nextPositionInPercent = privateState.TargetPositionInPercent;
                }
                else
                {
                    nextPositionInPercent -= dtPercent;
                    nextPosState = ActuatorPosition.MovingToPos1;
                }
            }
            else if (privateState.TargetPositionInPercent != privateState.PositionInPercent)
            {
                nextPositionInPercent = privateState.TargetPositionInPercent;
                nextPosState = ActuatorPosition.InBetween;
            }

            nextPositionInPercent = nextPositionInPercent.Clip(0.0, 100.0, privateState.TargetPositionInPercent);

            // generate the next PosState value to publish
            if (privateState.TargetPositionInPercent == nextPositionInPercent)
            {
                if (nextPositionInPercent <= 0.0)
                    nextPosState = ActuatorPosition.AtPos1;
                else if (nextPositionInPercent >= 100.0)
                    nextPosState = ActuatorPosition.AtPos2;
                else
                    nextPosState = ActuatorPosition.InBetween;
            }

            if (reset || (privateState.PosState != nextPosState && nextPosState != ActuatorPosition.None))
            {
                string entryStateStr = privateState.PosStateStr;

                privateState.PosState = nextPosState;
                privateState.PosStateStr = Config.ToString(nextPosState);
                privateState.PositionInPercent = nextPositionInPercent;

                logger.Info.Emit("State changed to {0} [{1} {2:f0}%] from {3}, target:{4}", privateState.PosStateStr, privateState.PosState, privateState.PositionInPercent, entryStateStr, privateState.TargetPosStr);

                forcePublish = true;
            }
            else if (privateState.PositionInPercent != nextPositionInPercent)
            {
                privateState.PositionInPercent = nextPositionInPercent;

                logger.Trace.Emit("Moving {0} [{1} {2:f0}%]", privateState.PosStateStr, privateState.PosState, privateState.PositionInPercent);

                forcePublish = true;
            }

            if (forcePublish || reset)
                PublishState();
        }

        public void SetTarget(ActuatorPosition targetPos, bool reset = false)
        {
            SetTarget(targetPos, GetTargetPositionInPercent(targetPos), reset: reset);
        }

        public double GetTargetPositionInPercent(ActuatorPosition targetPos, bool mapInBetweenToHalfWay = false)
        {
            return targetPos.GetTargetPositionInPercent(mapInBetweenToHalfWay: mapInBetweenToHalfWay, currentPositionInPercent: privateState.PositionInPercent);
        }

        public void SetTarget(double targetPositionInPercent, bool reset = false)
        {
            targetPositionInPercent = targetPositionInPercent.Clip(0.0, 100.0, 0.0);

            if (targetPositionInPercent == 0.0)
                SetTarget(ActuatorPosition.MoveToPos1, targetPositionInPercent, reset);
            else if (targetPositionInPercent == 100.0)
                SetTarget(ActuatorPosition.MoveToPos2, targetPositionInPercent, reset);
            else 
                SetTarget(ActuatorPosition.InBetween, targetPositionInPercent, reset);
        }

        public virtual void SetTarget(ActuatorPosition targetPos, double targetPositionInPercent, bool reset = false)
        {
            if (privateState.TargetPos != targetPos || privateState.TargetPositionInPercent != targetPositionInPercent || reset)
            {
                privateState.TargetPos = targetPos;
                privateState.TargetPosStr = Config.ToString(targetPos);
                privateState.TargetPositionInPercent = targetPositionInPercent;

                logger.Info.Emit("Target position changed to:{0} [{1} {2:f0}%]", privateState.TargetPosStr, privateState.TargetPos, privateState.TargetPositionInPercent);

                Service(TimeSpan.Zero, forcePublish: true, reset: reset);
            }
        }

        public void ResetStateTo(ActuatorPosition position)
        {
            SetTarget(position, reset: true);
        }

        public void AbortMove()
        {
            if (privateState.IsInMotion)
                SetTarget(ActuatorPosition.None);
        }

        protected void PublishState()
        {
            State = new ActuatorState(privateState);
        }

        public ActuatorConfig Config { get; protected set; }
        public TimeSpan Motion1To2Time { get { return Config.Motion1To2Time; } set { Config.Motion1To2Time = value; UpdateMotionRates(); } }
        public TimeSpan Motion2To1Time { get { return Config.Motion2To1Time; } set { Config.Motion2To1Time = value; UpdateMotionRates(); } }

        protected void UpdateMotionRates()
        {
            motion1To2PercentPerSecond = ((Motion1To2Time > TimeSpan.Zero) ? 100.0 / Motion1To2Time.TotalSeconds : 100.0);
            motion2To1PercentPerSecond = ((Motion2To1Time > TimeSpan.Zero) ? 100.0 / Motion2To1Time.TotalSeconds : 100.0);
        }

        private double motion1To2PercentPerSecond;
        private double motion2To1PercentPerSecond;

        public IActuatorState State { get; protected set; }

        protected ActuatorState privateState;
        protected Logging.ILogger logger;
    }
}

//-------------------------------------------------------------------
