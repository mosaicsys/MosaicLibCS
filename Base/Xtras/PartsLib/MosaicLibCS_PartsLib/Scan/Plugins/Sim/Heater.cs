//-------------------------------------------------------------------
/*! @file Heater.cs
 *  @brief 
 * 
 * Copyright (c) Mosaic Systems Inc.
 * Copyright (c) 2016 Mosaic Systems Inc.
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

using MosaicLib;
using MosaicLib.Utils;
using MosaicLib.Time;
using MosaicLib.PartsLib.Scan.ScanEngine;

namespace MosaicLib.PartsLib.Scan.Plugin.Sim.Heater
{
    /// <summary>
    /// This ScanEngine Plugin provides very basic simulated functionality of a single channel (single zone) heater
    /// <para/>Supports following channels:
    /// <para/>Inputs:  Setpoint, FaultInjection
    /// <para/>Outputs: Readback, OutputInPercent
    /// <para/>Supports the following config keys: (TBD)
    /// </summary>
    public class BasicHeaterSimScanEnginePlugin : ScanEnginePluginBase
    {
        public BasicHeaterSimScanEnginePlugin(string name) 
            : this(name, null) 
        {
        }

        public BasicHeaterSimScanEnginePlugin(string name, BasicHeaterSimScanEnginePluginConfig config) 
            : base(name)
        {
            Config = new BasicHeaterSimScanEnginePluginConfig(config);

            OutputValues = new BasicHeaterSimScanEnginePluginOutputs();
            OutputValues.slewLimitedSetpoint = Config.AmbientTemp;
            OutputValues.currentPower = 0.0;
            OutputValues.IntrinsicTemperature = Config.InitialTemp;
            OutputValues.Readback = OutputValues.IntrinsicTemperature;
        }

        public BasicHeaterSimScanEnginePluginConfig Config { get; protected set; }

        public double Setpoint { get; set; }

        public string FaultInjectionStr
        {
            get { return _faultInjectionStr; }
            set
            {
                value = value.MapNullToEmpty();
                if (_faultInjectionStr != value)
                {
                    _faultInjectionStr = value;
                    FaultInjection = Utils.Enum.TryParse(value, Common.FaultInjection.None);
                }
            }
        }
        private string _faultInjectionStr = string.Empty;

        public Common.FaultInjection FaultInjection { get; set; }

        public BasicHeaterSimScanEnginePluginOutputs OutputValues { get; private set; }

        public override void Service(TimeSpan measuredServiceInterval, QpcTimeStamp scanStartTime)
        {
            double measuredServiceIntervalInSec = measuredServiceInterval.TotalSeconds;

            Common.FaultInjection faultInjection = FaultInjection;

            bool isOnline = OutputValues.IsOnline = !((faultInjection & Common.FaultInjection.Offline) != default(Common.FaultInjection));
            bool sensorFault = OutputValues.SensorFault = ((faultInjection & Common.FaultInjection.SensorFault) != default(Common.FaultInjection));

            double prevSlewLimitedSetpoint = OutputValues.slewLimitedSetpoint;
            SlewRateLimitControl(ref OutputValues.slewLimitedSetpoint, Setpoint, Config.MaxHeatRampRatePerSec, Config.MaxCoolRampRatePerSec, measuredServiceIntervalInSec);

            double setpointRatePerSec = ((measuredServiceIntervalInSec > 0.0) ? ((OutputValues.slewLimitedSetpoint - prevSlewLimitedSetpoint) / measuredServiceIntervalInSec) : 0.0);

            if (!sensorFault)
                OutputValues.Readback = OutputValues.IntrinsicTemperature + (Config.EnableNoise ? Rng.GetNextRandomInMinus1ToPlus1Range() * Config.ReadbackNoiseLevel : 0.0);
            else
                OutputValues.Readback = Config.SensorFaultReadback;

            double clippedSetpointLessIntrinsic = (OutputValues.slewLimitedSetpoint - OutputValues.IntrinsicTemperature).Clip(-Config.ErrorClip, Config.ErrorClip);

            double nominalRequiredLoadPower = (OutputValues.Readback + clippedSetpointLessIntrinsic - Config.AmbientTemp) * Config.ThermalLoadWattsPerDeg;
            double nominalRequiredRampPower = setpointRatePerSec * 1.0 / Config.ThermalMassDegPerSecPerWatt;

            if (isOnline)
            {
                SlewRateLimitControl(ref OutputValues.currentPower, nominalRequiredRampPower + nominalRequiredLoadPower, Config.PhysicalPowerRampLimitWattsPerSec, Config.PhysicalPowerRampLimitWattsPerSec, measuredServiceIntervalInSec);
                OutputValues.currentPower = OutputValues.currentPower.Clip(0.0, Config.FullScalePowerWatts);
            }
            else
            {
                OutputValues.currentPower = 0.0;
            }

            double currentPowerWithNoise = OutputValues.currentPower;
            if (isOnline && Config.EnableNoise && OutputValues.currentPower >= Config.PowerNoiseLevel)
                currentPowerWithNoise = OutputValues.currentPower + Config.PowerNoiseLevel * Rng.GetNextRandomInMinus1ToPlus1Range();

            double intrinsictAmbientLoadInWatts = (OutputValues.IntrinsicTemperature - Config.AmbientTemp) * Config.ThermalLoadWattsPerDeg;     // this is a signed value since it represents heat conductivity between the thermal mass and the ambient
            double currentLessAmbientInWatts = currentPowerWithNoise - intrinsictAmbientLoadInWatts;

            double deltaTRate = currentLessAmbientInWatts * Config.ThermalMassDegPerSecPerWatt;
            double deltaT = deltaTRate * measuredServiceIntervalInSec;

            OutputValues.IntrinsicTemperature = (OutputValues.IntrinsicTemperature + deltaT).Clip(0.0, 1200.0);
            OutputValues.OutputInPercent = 100.0 * currentPowerWithNoise / Config.FullScalePowerWatts;
        }

        public void SlewRateLimitControl(ref double slewLimitedValue, double targetValue, double increaseRateLimit, double decreaseRateLimit, double deltaTimeInSeconds)
        {
            double maxIncrease = increaseRateLimit * deltaTimeInSeconds;
            double maxDecrease = decreaseRateLimit * deltaTimeInSeconds;

            if (targetValue > slewLimitedValue + maxIncrease)
                slewLimitedValue += maxIncrease;
            else if (targetValue < slewLimitedValue - maxDecrease)
                slewLimitedValue -= maxDecrease;
            else
                slewLimitedValue = targetValue;
        }
    }

    /// <summary>
    /// Used with <see cref="BasicHeaterSimScanEnginePlugin"/> as TConfigValueSetType.  
    /// Defines the values that are used to configure how a specific actuator behaves.
    /// </summary>
    public class BasicHeaterSimScanEnginePluginConfig
    {
        public BasicHeaterSimScanEnginePluginConfig()
        {
			InitialTemp = 25.0;
			AmbientTemp = 25.0;
            MaxHeatRampRatePerSec = 5.0;
            MaxCoolRampRatePerSec = 2.0;
            ErrorClip = 1000.0;
			ThermalMassDegPerSecPerWatt = 0.010;
			ThermalLoadWattsPerDeg = 7.5;				// 3000 watts required to sustain 400 Deg
			FullScalePowerWatts = 5000;
            PhysicalPowerRampLimitWattsPerSec = 5000.0;
			EnableNoise = true;
			ReadbackNoiseLevel = 0.02;
            PowerNoiseLevel = 0.003 * FullScalePowerWatts;
            SensorFaultReadback = 1350.0;       // standard maximum temperature for a K type thermouple - happens about 55 mVolts.
        }

        public BasicHeaterSimScanEnginePluginConfig(BasicHeaterSimScanEnginePluginConfig other)
        {
            other = other ?? emptyConfig;

            InitialTemp = other.InitialTemp;
            AmbientTemp = other.AmbientTemp;
            MaxHeatRampRatePerSec = other.MaxHeatRampRatePerSec;
            MaxCoolRampRatePerSec = other.MaxCoolRampRatePerSec;
            ErrorClip = other.ErrorClip;
            ThermalMassDegPerSecPerWatt = other.ThermalMassDegPerSecPerWatt;
            ThermalLoadWattsPerDeg = other.ThermalLoadWattsPerDeg;
            FullScalePowerWatts = other.FullScalePowerWatts;
            PhysicalPowerRampLimitWattsPerSec = other.PhysicalPowerRampLimitWattsPerSec;
            EnableNoise = other.EnableNoise;
        }

		
        public double InitialTemp { get; set; }

        public double AmbientTemp { get; set; }

        public double MaxHeatRampRatePerSec { get; set; }

        public double MaxCoolRampRatePerSec { get; set; } 

        public double ErrorClip { get; set; }

		public double ThermalMassDegPerSecPerWatt { get; set; }

		public double ThermalLoadWattsPerDeg { get; set; }

		public double FullScalePowerWatts { get; set; }

        public double PhysicalPowerRampLimitWattsPerSec { get; set; }
		
		public bool EnableNoise { get; set; }

		public double ReadbackNoiseLevel { get; set; }

        public double PowerNoiseLevel { get; set; }

        public double SensorFaultReadback { get; set; }

        private static readonly BasicHeaterSimScanEnginePluginConfig emptyConfig = new BasicHeaterSimScanEnginePluginConfig();
    }

    /// <summary>Used with <see cref="BasicHeaterSimScanEnginePlugin"/> as TOutputValueSetType</summary>
    public class BasicHeaterSimScanEnginePluginOutputs
    {
		public bool SensorFault { get; set; }

        public bool IsOnline { get; set; }

        public double slewLimitedSetpoint;

        public double currentPower;

        public double IntrinsicTemperature { get; set; }
			
        public double Readback { get; set; }

        public double OutputInPercent { get; set; }
		
        public String StateStr { get; set; }
    }
}

//-------------------------------------------------------------------
