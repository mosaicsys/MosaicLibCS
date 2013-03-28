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

namespace MosaicLib.PartsLib.Helpers
{
    using System;
    using System.Runtime.Serialization;
    using System.Collections.Generic;
    using MosaicLib.Time;

    [DataContract]
    public enum ActuatorPosition
    {
        [EnumMember]
        Undefined,
        [EnumMember]
        None,
        [EnumMember]
        AtPos1,
        [EnumMember]
        MovingToPos1,
        [EnumMember]
        AtPos2,
        [EnumMember]
        MovingToPos2,
        [EnumMember]
        Inbetween,
        [EnumMember]
        Fault
    }

    [DataContract]
    public class ActuatorConfig
    {
        public ActuatorConfig(string name, string pos1Name, string pos2Name, TimeSpan motionTime) : this(name, pos1Name, pos2Name, motionTime, motionTime, ActuatorPosition.AtPos1) { }
        public ActuatorConfig(string name, string pos1Name, string pos2Name, TimeSpan motionTime, ActuatorPosition initialPos) : this(name, pos1Name, pos2Name, motionTime, motionTime, initialPos) { }
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
        public string Name { get; protected set; }
        [DataMember]
        public string Pos1Name { get; protected set; }
        [DataMember]
        public string Pos2Name { get; protected set; }
        [DataMember]
        public TimeSpan Motion1To2Time { get; protected set; }
        [DataMember]
        public TimeSpan Motion2To1Time { get; protected set; }
        [DataMember]
        public ActuatorPosition InitialPos { get; protected set; }

        public string ToString(ActuatorPosition pos)
        {
            switch (pos)
            {
                case ActuatorPosition.AtPos1: return Pos1Name;
                case ActuatorPosition.AtPos2: return Pos2Name;
                case ActuatorPosition.MovingToPos1: return "MovingTo" + Pos1Name;
                case ActuatorPosition.MovingToPos2: return "MovingTo" + Pos2Name;
                case ActuatorPosition.Undefined:
                case ActuatorPosition.None:
                case ActuatorPosition.Inbetween:
                case ActuatorPosition.Fault:
                default:
                    return pos.ToString();
            }
        }
    }

    [DataContract]
    public class ActuatorState
    {
        public ActuatorState()
        {
            TargetPos = ActuatorPosition.None;
            TargetPosStr = TargetPos.ToString();
            PosState = ActuatorPosition.Undefined;
            PosStateStr = PosState.ToString();
            TimeStamp = QpcTimeStamp.Now;
        }

        public ActuatorState(ActuatorState rhs)
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
        public bool IsAtTarget { get { return (TargetPos == ActuatorPosition.None || TargetPos == PosState); } }
        public bool IsInMotion { get { return (PosState == ActuatorPosition.MovingToPos1 || PosState == ActuatorPosition.MovingToPos2); } }
        public static bool IsTargetPositionValid(ActuatorPosition pos) { return (pos == ActuatorPosition.AtPos1 || pos == ActuatorPosition.AtPos2 || pos == ActuatorPosition.None); }
        public static bool IsActuatorPositionValid(ActuatorPosition pos) { return (pos == ActuatorPosition.AtPos1 || pos == ActuatorPosition.AtPos2 || pos == ActuatorPosition.MovingToPos1 || pos == ActuatorPosition.MovingToPos2 || pos == ActuatorPosition.Inbetween); }
        public bool IsValid { get { return (IsTargetPositionValid(TargetPos) && IsActuatorPositionValid(PosState)); } } 
    }

    public class ActuatorBase
    {
        public ActuatorBase(ActuatorConfig config, ActuatorState state) 
        {
            Config = config;
            PrivateState = state;

            logger = new Logging.Logger(config.Name, Logging.LogGate.All);

            PrivateState.TargetPos = PrivateState.PosState = config.InitialPos;
            PrivateState.TargetPosStr = PrivateState.PosStateStr = config.ToString(config.InitialPos);
            PrivateState.TimeStamp = QpcTimeStamp.Now;

            logger.Info.Emit("Initial state is:{0} [{1}]", PrivateState.PosStateStr, PrivateState.PosState);

            PublishState();
        }

        public void Service(QpcTimeStamp now) { Service(false, now); }

        protected void Service(bool forcePublish, QpcTimeStamp now)
        {
            if (PrivateState.IsAtTarget)
                return;

            ActuatorPosition nextState = ActuatorPosition.None;

            switch (PrivateState.TargetPos)
            {
                case ActuatorPosition.AtPos1:
                    if (PrivateState.PosState != ActuatorPosition.MovingToPos1)
                        nextState = ActuatorPosition.MovingToPos1;
                    else if (PrivateState.TimeInState >= Config.Motion2To1Time.TotalSeconds)
                        nextState = PrivateState.TargetPos;
                    break;
                case ActuatorPosition.AtPos2:
                    if (PrivateState.PosState != ActuatorPosition.MovingToPos2)
                        nextState = ActuatorPosition.MovingToPos2;
                    else if (PrivateState.TimeInState >= Config.Motion1To2Time.TotalSeconds)
                        nextState = PrivateState.TargetPos;
                    break;
                case ActuatorPosition.None:
                    if (PrivateState.IsInMotion)
                        nextState = ActuatorPosition.Inbetween;
                    break;  // do not change the current state
                default:
                    nextState = PrivateState.TargetPos;
                    break;
            }

            if (PrivateState.PosState != nextState && nextState != ActuatorPosition.None)
            {
                ActuatorPosition entryState = PrivateState.PosState;
                string entryStateStr = PrivateState.PosStateStr;

                PrivateState.PosState = nextState;
                PrivateState.PosStateStr = Config.ToString(nextState);
                PrivateState.TimeStamp = now;

                logger.Info.Emit("State changed to {0} [{1}] from {2}, target:{3}", PrivateState.PosStateStr, PrivateState.PosState, entryStateStr, PrivateState.TargetPosStr);
                forcePublish = true;
            }

            if (forcePublish)
                PublishState();
        }

        public void SetTarget(ActuatorPosition targetPos)
        {
            if (PrivateState.TargetPos != targetPos)
            {
                PrivateState.TargetPos = targetPos;
                PrivateState.TargetPosStr = Config.ToString(targetPos);

                logger.Info.Emit("Target position changed to:{0} [{1}]", PrivateState.TargetPosStr, PrivateState.TargetPos);

                Service(true, QpcTimeStamp.Now);
            }
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
