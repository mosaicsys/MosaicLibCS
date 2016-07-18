//-------------------------------------------------------------------
/*! @file Actuator.cs
 *  @brief Provides a simple model of a two position actuator
 * 
 * Copyright (c) Mosaic Systems Inc., All rights reserved
 * Copyright (c) 2011 Mosaic Systems Inc., All rights reserved
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
//-------------------------------------------------------------------

using System;
using System.Runtime.Serialization;
using System.Collections.Generic;
using MosaicLib.Time;

namespace MosaicLib.PartsLib.Helpers
{
    /// <summary>
    /// Defines the positions that the ActuatorBase can be in/moved to
    /// <para/>Valid targets: MoveToPos1, MoveToPos2, Inbetween, None
    /// <para/>Other known states: AtPos1, AtPos2, MovingToPos1, MovingToPos2, Undefined (default, 0), Unknown, Fault
    /// </summary>
    [DataContract]
    public enum ActuatorPosition : int
    {
        [EnumMember]
        Undefined = 0,
        [EnumMember]
        AtPos1 = 1,
        [EnumMember]
        AtPos2 = 2,
        [EnumMember]
        MovingToPos1 = 3,
        [EnumMember]
        MovingToPos2 = 4,
        [EnumMember]
        InBetween = 5,
        [EnumMember]
        None = 6,
        [EnumMember]
        Unknown = 7,
        [EnumMember]
        Fault = 8,

        [EnumMember]
        MoveToPos1 = AtPos1,
        [EnumMember]
        MoveToPos2 = AtPos2,
    }

    public static partial class ExtensionMethods
    {
        /// <summary>Returns true if the given pos is AtPos1</summary>
        public static bool IsAtPos1(this ActuatorPosition pos)  { return (pos == ActuatorPosition.AtPos1); }
        /// <summary>Returns true if the given pos is AtPos2</summary>
        public static bool IsAtPos2(this ActuatorPosition pos) { return (pos == ActuatorPosition.AtPos2); }
        /// <summary>Returns true if the given pos is AtPos2</summary>
        public static bool IsInBetween(this ActuatorPosition pos) { return (pos == ActuatorPosition.InBetween); }
        /// <summary>Returns true if the given pos is MovingToPos1 or MovingToPos2</summary>
        public static bool IsInMotion(this ActuatorPosition pos) { return (pos == ActuatorPosition.MovingToPos1 || pos == ActuatorPosition.MovingToPos2); }
        /// <summary>Returns true if the given targetPos is AtPos1, AtPos2, or None</summary>
        public static bool IsTargetPositionValid(this ActuatorPosition targetPos) { return (targetPos == ActuatorPosition.MoveToPos1|| targetPos == ActuatorPosition.MoveToPos2 || targetPos == ActuatorPosition.None); }
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
    }

    [DataContract]
    public class ActuatorConfig
    {
        public ActuatorConfig() 
        {
            InitialPos = ActuatorPosition.Unknown;
        }

        public ActuatorConfig(string name, string pos1Name, string pos2Name, TimeSpan motionTime) 
            : this(name, pos1Name, pos2Name, motionTime, motionTime, ActuatorPosition.AtPos1) 
        { }

        public ActuatorConfig(string name, string pos1Name, string pos2Name, TimeSpan motionTime, ActuatorPosition initialPos) 
            : this(name, pos1Name, pos2Name, motionTime, motionTime, initialPos) 
        { }

        public ActuatorConfig(string name, string pos1Name, string pos2Name, TimeSpan motion1To2Time, TimeSpan motion2To1Time, ActuatorPosition initialPos)
        {
            Name = name;
            Pos1Name = pos1Name;
            Pos2Name = pos2Name;
            Motion1To2Time = motion1To2Time;
            Motion2To1Time = motion2To1Time;
            InitialPos = initialPos;
        }

        public ActuatorConfig(ActuatorConfig rhs)
        {
            Name = rhs.Name;
            Pos1Name = rhs.Pos1Name;
            Pos2Name = rhs.Pos2Name;
            Motion1To2Time = rhs.Motion1To2Time;
            Motion2To1Time = rhs.Motion2To1Time;
            InitialPos = rhs.InitialPos;
        }

        [DataMember]
        public string Name { get; set; }
        [DataMember]
        public string Pos1Name { get; set; }
        [DataMember]
        public string Pos2Name { get; set; }
        [DataMember]
        public TimeSpan Motion1To2Time { get; set; }
        [DataMember]
        public TimeSpan Motion2To1Time { get; set; }
        [DataMember]
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
    }

    public interface IActuatorState
    {
        ActuatorPosition TargetPos { get; }
        string TargetPosStr { get; }

        ActuatorPosition PosState { get; }
        string PosStateStr { get; }

        QpcTimeStamp TimeStamp { get; }

        bool IsEqualTo(IActuatorState rhs);

        double TimeInState { get; }

        bool IsAtPos1 { get; }
        bool IsAtPos2 { get; }
        bool IsAtTarget { get; }
        bool IsTargetNone { get; }
        bool IsInMotion { get; }
        bool IsValid { get; }
    }

    [DataContract]
    public class ActuatorState : IActuatorState
    {
        public ActuatorState()
        {
            TargetPos = ActuatorPosition.None;
            TargetPosStr = TargetPos.ToString();
            PosState = ActuatorPosition.Undefined;
            PosStateStr = PosState.ToString();
            TimeStamp = QpcTimeStamp.Now;
        }

        public ActuatorState(IActuatorState rhs)
        {
            TargetPos = rhs.TargetPos;
            TargetPosStr = rhs.TargetPosStr;
            PosState = rhs.PosState;
            PosStateStr = rhs.PosStateStr;
            TimeStamp = rhs.TimeStamp;
        }

        [DataMember]
        public ActuatorPosition TargetPos { get; set; }

        [DataMember]
        public string TargetPosStr { get; set; }

        [DataMember]
        public ActuatorPosition PosState { get; set; }

        [DataMember]
        public string PosStateStr { get; set; }

        public QpcTimeStamp TimeStamp { get; set; }

        [DataMember]
        public double TimeInState { get { return TimeStamp.Age.TotalSeconds; } set { TimeStamp = QpcTimeStamp.Now + TimeSpan.FromSeconds(value); } }

        public bool IsAtPos1 { get { return (IsAtTarget && PosState == ActuatorPosition.AtPos1); } }
        public bool IsAtPos2 { get { return (IsAtTarget && PosState == ActuatorPosition.AtPos2); } }
        public bool IsAtTarget { get { return ((IsTargetNone && !IsInMotion) || (TargetPos == PosState)); } }
        public bool IsTargetNone { get { return (TargetPos == ActuatorPosition.None); } }
        public bool IsInMotion { get { return (PosState == ActuatorPosition.MovingToPos1 || PosState == ActuatorPosition.MovingToPos2); } }
        public bool IsValid { get { return (TargetPos.IsTargetPositionValid() && PosState.IsValid()); } }

        public bool IsEqualTo(IActuatorState rhs)
        {
            return (rhs != null
                    && TargetPos == rhs.TargetPos
                    && TargetPosStr == rhs.TargetPosStr
                    && PosState == rhs.PosState
                    && PosStateStr == rhs.PosStateStr
                    && TimeStamp == rhs.TimeStamp
                    );
        }

        public override string ToString()
        {
            return PosStateStr;
        }
    }

    public class ActuatorBase
    {
        public ActuatorBase(ActuatorConfig config, ActuatorState state) 
        {
            Config = new ActuatorConfig(config);
            PrivateState = new ActuatorState(state);

            logger = new Logging.Logger(config.Name, Logging.LogGate.All);

            PrivateState.TargetPos = PrivateState.PosState = config.InitialPos;
            PrivateState.TargetPosStr = PrivateState.PosStateStr = config.ToString(config.InitialPos);
            PrivateState.TimeStamp = QpcTimeStamp.Now;

            logger.Info.Emit("Initial state is:{0} [{1}]", PrivateState.PosStateStr, PrivateState.PosState);

            PublishState();
        }

        public void Service(QpcTimeStamp now) { Service(false, now, false); }

        protected void Service(bool forcePublish, QpcTimeStamp now, bool reset)
        {
            if (PrivateState.IsAtTarget && !reset)
                return;

            ActuatorPosition nextState = ActuatorPosition.None;

            switch (PrivateState.TargetPos)
            {
                case ActuatorPosition.AtPos1:
                    if (reset)
                        nextState = PrivateState.TargetPos;
                    else if (PrivateState.PosState != ActuatorPosition.MovingToPos1)
                        nextState = ActuatorPosition.MovingToPos1;
                    else if (PrivateState.TimeInState >= Config.Motion2To1Time.TotalSeconds)
                        nextState = PrivateState.TargetPos;
                    break;
                case ActuatorPosition.AtPos2:
                    if (reset)
                        nextState = PrivateState.TargetPos;
                    else if (PrivateState.PosState != ActuatorPosition.MovingToPos2)
                        nextState = ActuatorPosition.MovingToPos2;
                    else if (PrivateState.TimeInState >= Config.Motion1To2Time.TotalSeconds)
                        nextState = PrivateState.TargetPos;
                    break;
                case ActuatorPosition.None:
                    if (reset)
                        nextState = PrivateState.TargetPos;
                    else if (PrivateState.IsInMotion)
                        nextState = ActuatorPosition.InBetween;
                    break;  // do not change the current state
                default:
                    nextState = PrivateState.TargetPos;
                    break;
            }

            if (reset || (PrivateState.PosState != nextState && nextState != ActuatorPosition.None))
            {
                ActuatorPosition entryState = PrivateState.PosState;
                string entryStateStr = PrivateState.PosStateStr;

                PrivateState.PosState = nextState;
                PrivateState.PosStateStr = Config.ToString(nextState);
                PrivateState.TimeStamp = now;

                logger.Info.Emit("State changed to {0} [{1}] from {2}, target:{3}", PrivateState.PosStateStr, PrivateState.PosState, entryStateStr, PrivateState.TargetPosStr);
                forcePublish = true;
            }

            if (forcePublish || reset)
                PublishState();
        }

        public void SetTarget(ActuatorPosition targetPos)
        {
            SetTarget(targetPos, false);
        }

        public void SetTarget(ActuatorPosition targetPos, bool reset)
        {
            if (PrivateState.TargetPos != targetPos || reset)
            {
                PrivateState.TargetPos = targetPos;
                PrivateState.TargetPosStr = Config.ToString(targetPos);

                logger.Info.Emit("Target position changed to:{0} [{1}]", PrivateState.TargetPosStr, PrivateState.TargetPos);

                Service(true, QpcTimeStamp.Now, reset);
            }
        }

        public void ResetStateTo(ActuatorPosition position)
        {
            SetTarget(position, true);
        }

        public void AbortMove()
        {
            if (PrivateState.IsInMotion)
                SetTarget(ActuatorPosition.None);
        }

        void PublishState()
        {
            State = new ActuatorState(PrivateState);
        }

        public ActuatorConfig Config { get; protected set; }
        protected ActuatorState PrivateState { get; set; }
        public ActuatorState State { get; protected set; }
        Logging.ILogger logger;
    }
}

//-------------------------------------------------------------------
