//-------------------------------------------------------------------
/*! @file Physics.cs
 *  @brief 
 * 
 * Copyright (c) Mosaic Systems Inc.
 * Copyright (c) 2017 Mosaic Systems Inc.
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
using System.Collections.Generic;
using System.Linq;

using MosaicLib;
using MosaicLib.PartsLib.Common.MassFlow;
using MosaicLib.Time;
using MosaicLib.Utils;

namespace MosaicLib.PartsLib.Common.Physics
{
	namespace Gasses
	{
		public static class Air
		{
            /// <summary>Standard atmospheric pressure in kPa [101.325]</summary>
            public const double StandardAtmPressureInKPa = Physics.UnitsOfMeasure.Constants.StdAtmInKPa;
		}

		public static class N2
		{
            /// <summary>kPa s at 20.0C [1.76e-8]</summary>
			public const double ViscosityInKPaS = 1.76e-8;

            /// <summary>kg per m^3 [1.165]</summary>
			public const double DensityInKGperM3 = 1.165;
		}

        public static partial class Constants
        {
            /// <summary>per Mol [6.022140857e23]</summary>
            public const double AvogadroPerMol = 6.022140857e23;

            /// <summary>J (kg m^2/s^2) per (DegK * mol) [1.38064852e-23]</summary>
            public const double Boltzmann = 1.38064852e-23;

            /// <summary>J (kg m^2/s^2) per (mol DegK) aka R [8.3144598]</summary>
            public const double IdealGasConstant = 8.3144598;
        }

        ///<summary>
        ///This static class defines methods that are used to solve for each one of the 4 variables in the Ideal Gas Law:
        /// Pressure, Volume, moles, Temperature (DegK)
        ///</summary>
        /// <remarks>
        /// P V = n R T
        /// n = (P * V)/(R * T)
        ///   = (kg m/s^2 / m^2 * m^3)/((kg m^2/s^2) / DegK / mol) * DegK)
        ///   = (m^2)/(m^2) = mol
        /// V = (n R T)/P
        /// P = (n R T)/V
        /// T = (P V)/(n R)
        /// </remarks>
        public static class IdealGasLaw
        {
            /// <summary>
            /// returns n = (P * V)/(R * T)
            /// </summary>
            public static double ToMoles(double pressureInKPa, double volumeInM3, double temperatureInDegK)
            {
                if (temperatureInDegK > 0.0)
                    return (pressureInKPa * volumeInM3) / (Constants.IdealGasConstant * temperatureInDegK);
                else
                    return 0.0;
            }

            /// <summary>
            /// returns P = (n R T)/V
            /// </summary>
            public static double ToPressureInKPa(double moles, double volumeInM3, double temperatureInDegK)
            {
                if (volumeInM3 > 0.0)
                    return (moles * Constants.IdealGasConstant * temperatureInDegK) / (volumeInM3);
                else
                    return 0.0;
            }

            /// <summary>
            /// returns V = (n R T)/P
            /// </summary>
            public static double ToVolumeInM3(double moles, double pressureInKPa, double temperatureInDegK)
            {
                if (pressureInKPa > 0.0)
                    return (moles * Constants.IdealGasConstant * temperatureInDegK) / (pressureInKPa);
                else
                    return 0.0;
            }

            /// <summary>
            /// returns T = (P V)/(n R)
            /// </summary>
            public static double ToTemperatureInDegK(double moles, double pressureInKPa, double volumeInM3)
            {
                if (moles > 0.0)
                    return (pressureInKPa * volumeInM3) / (moles * Constants.IdealGasConstant);
                else
                    return 0.0;
            }
        }
	}

    namespace UnitsOfMeasure
    {
        #region Temperature (DegK, DegC, DegF)

        namespace Temperature
        {
            /// <summary>Base class for temperature Unit of measure helper types.</summary>
            public class Temperature : IComparable<Temperature>, IEquatable<Temperature>
            {
                public double Value { get; set; }
                public TemperatureUnits Units { get; protected set; }
                public Temperature ConvertToUnits(TemperatureUnits toUnits) { return new Temperature(this, toUnits); }

                public Temperature(Temperature other) : this(other.Value, other.Units) { }
                protected Temperature(double value, TemperatureUnits units) { Value = value; Units = units; }
                protected Temperature(Temperature fromTemperature, TemperatureUnits toUnits) : this(fromTemperature.Value.ConvertUnits(fromTemperature.Units, toUnits), toUnits) { }

                public override string ToString() { return "{0:g3} {1}".CheckedFormat(Value, Units); }

                public static explicit operator double(Temperature value) { return value.Value; }
                public static explicit operator TemperatureUnits(Temperature value) { return value.Units; }

                public static Temperature operator +(Temperature a, Temperature b)
                {
                    return (a.Units == b.Units) ? (new Temperature(a.Value + b.Value, a.Units)) : (a + b.ConvertToUnits(a.Units));
                }

                public static Temperature operator -(Temperature a, Temperature b)
                {
                    return (a.Units == b.Units) ? (new Temperature(a.Value - b.Value, a.Units)) : (a - b.ConvertToUnits(a.Units));
                }

                public int CompareTo(Temperature other)
                {
                    return (Units == other.Units) ? Value.CompareTo(other.Value) : this.CompareTo(other.ConvertToUnits(Units));
                }

                public bool Equals(Temperature other)
                {
                    return (other != null && ((Units == other.Units && Value == other.Value) || (Units != other.Units && this.Equals(other.ConvertToUnits(Units)))));
                }
            }

            /// <summary>Unit of measure helper type.  Represents a double value in TemperatureUnits.DegK.  implicit and explicit casts are used to convert between double and this UOM as well as between values with related UOMs</summary>
            public class DegK : Temperature
            {
                private const TemperatureUnits units = TemperatureUnits.DegK;

                public DegK(double value = 0.0) : base(value, units) { }
                public DegK(Temperature fromTemperature) : base(fromTemperature, units) { }

                public static explicit operator DegK(double value) { return new DegK(value); }
            }

            /// <summary>Unit of measure helper type.  Represents a double value in TemperatureUnits.DegC.  implicit and explicit casts are used to convert between double and this UOM as well as between values with related UOMs</summary>
            public class DegC : Temperature
            {
                private const TemperatureUnits units = TemperatureUnits.DegC;

                public DegC(double value = 0.0) : base(value, units) { }
                public DegC(Temperature fromTemperature) : base(fromTemperature, units) { }

                public static explicit operator DegC(double value) { return new DegC(value); }
            }

            /// <summary>Unit of measure helper type.  Represents a double value in TemperatureUnits.DegF.  implicit and explicit casts are used to convert between double and this UOM as well as between values with related UOMs</summary>
            public class DegF : Temperature
            {
                private const TemperatureUnits units = TemperatureUnits.DegF;

                public DegF(double value = 0.0) : base(value, units) { }
                public DegF(Temperature fromTemperature) : base(fromTemperature, units) { }

                public static explicit operator DegF(double value) { return new DegF(value); }
            }
        }

        #endregion

        #region Temperature (TemperatureUnits, Constants, ExtensionMethods - conversions)

        /// <summary>
        /// Temperature units of measure
        /// <para/>None (0), DegK (1), DegC (2), DegF (3)
        /// </summary>
        public enum TemperatureUnits : int
        {
            /// <summary>No Temperature Units of Measure have been provided (0) - placeholder</summary>
            None = 0,

            /// <summary>Degrees Kelvin (2)</summary>
            DegK = 1,

            /// <summary>Degrees Celsius (2)</summary>
            DegC = 2,

            /// <summary>Degrees Fahrenheit (3)</summary>
            DegF = 3,
        }

        public static partial class Constants
        {
            /// <summary>K = C + 273.15</summary>
            public const double DegKOffsetFromDegC = 273.15;

            /// <summary>-459.67 F when K is zero</summary>
            public const double DegFOffsetAtZeroDegK = -459.67;

            /// <summary>32.0 F when C is zero</summary>
            public const double DegFOffsetAtZeroDegC = 32.0;

            /// <summary>Standard "room" temperature in DegC [20.0]</summary>
            public const double StandardTemperatureInDegC = 20.0;

            /// <summary>Standard "room" temperature in DegK [20.0 + 273.15]</summary>
            public const double StandardTemperatureInDegK = 20.0 + DegKOffsetFromDegC;
        }

        public static partial class ExtensionMethods
        {
            /// <summary>
            /// Returns true if the given value is None
            /// </summary>
            public static bool IsNone(this TemperatureUnits value)
            {
                return (value == TemperatureUnits.None);
            }

            /// <summary>
            /// Converts the given valueInUserUnits from the given fromUOM TemperatureUnits into Degrees Kelvin and returns it.
            /// </summary>
            public static double ConvertToDegK(this TemperatureUnits fromUOM, double valueInUserUnits)
            {
                switch (fromUOM)
                {
                    case TemperatureUnits.DegK: return valueInUserUnits;
                    case TemperatureUnits.DegC: return (valueInUserUnits + Constants.DegKOffsetFromDegC);
                    case TemperatureUnits.DegF: return (valueInUserUnits.ConvertDegFToDegC() + Constants.DegKOffsetFromDegC);
                    default: return 0.0;
                }
            }

            /// <summary>
            /// Converts the given valueInDegK in Degrees Kelvin into a value of the given toUOM TemperatureUnits and returns it.
            /// </summary>
            public static double ConvertFromDegK(this TemperatureUnits toUOM, double valueInDegK)
            {
                switch (toUOM)
                {
                    case TemperatureUnits.DegK: return valueInDegK;
                    case TemperatureUnits.DegC: return (valueInDegK - Constants.DegKOffsetFromDegC);
                    case TemperatureUnits.DegF: return (valueInDegK - Constants.DegKOffsetFromDegC).ConvertDegCToDegF();
                    default: return 0.0;
                }
            }

            /// <summary>
            /// Converts the given <paramref name="value"/> measured in <paramref name="fromUOM"/> TemperatureUnits into a value of the given <paramref name="toUOM"/> TemperatureUnits and returns it.
            /// </summary>
            public static double ConvertUnits(this double value, TemperatureUnits fromUOM, TemperatureUnits toUOM)
            {
                if (fromUOM == toUOM)
                    return value;

                return toUOM.ConvertFromDegK(fromUOM.ConvertToDegK(value));
            }

            /// <summary>
            /// Converts the given <paramref name="value"/> measured in <paramref name="fromUOM"/> TemperatureUnits into a value of the given <paramref name="toUOM"/> TemperatureUnits and returns it.
            /// </summary>
            [Obsolete("Please replace use of this method with the use of the less ambiguous value.ConvertUnits method that has a consistent signature accross the different UOM values (2018-11-11)")]
            public static double ConvertUnits(this TemperatureUnits fromUOM, double value, TemperatureUnits toUOM)
            {
                return value.ConvertUnits(fromUOM, toUOM);
            }

            /// <summary>
            /// Converts the given <paramref name="value"/> measured in <paramref name="fromUOM"/> TemperatureUnits into a value of the given <paramref name="toUOM"/> PressureUnits and returns it.
            /// </summary>
            [Obsolete("Please replace use of this method with the use of the less ambiguous value.ConvertUnits method that has a consistent signature accross the different UOM values (2018-11-11)")]
            public static double ConvertUnits(this TemperatureUnits toUOM, TemperatureUnits fromUOM, double value)
            {
                return value.ConvertUnits(fromUOM, toUOM);
            }

            /// <summary>
            /// Returns (((valueInDegF - 32.0) * 5.0) / 9.0)
            /// </summary>
            public static double ConvertDegFToDegC(this double valueInDegF)
            {
                return (((valueInDegF - 32.0) * 5.0) / 9.0);
            }

            /// <summary>
            /// Returns (((valueInDegC * 9.0) / 5.0) + 32.0)
            /// </summary>
            public static double ConvertDegCToDegF(this double valueInDegC)
            {
                return (((valueInDegC * 9.0) / 5.0) + 32.0);
            }
        }

        #endregion

        #region Pressure (KPa, Torr, mTorr, Bar, mBar, PSI, Atm)

        namespace Pressure
        {
            /// <summary>Base class for pressure Unit of measure helper types.</summary>
            public class Pressure : IComparable<Pressure>, IEquatable<Pressure>
            {
                public double Value { get; set; }
                public PressureUnits Units { get; protected set; }
                public Pressure ConvertToUnits(PressureUnits toUnits) { return new Pressure(this, toUnits); }

                public Pressure(Pressure other) : this(other.Value, other.Units) { }
                protected Pressure(double value, PressureUnits units) { Value = value; Units = units; }
                protected Pressure(Pressure fromPressure, PressureUnits units) : this(fromPressure.Value.ConvertUnits(fromPressure.Units, units), units) { }

                public override string ToString() { return "{0:g6} {1}".CheckedFormat(Value, Units); }

                public static explicit operator double(Pressure value) { return value.Value; }
                public static explicit operator PressureUnits(Pressure value) { return value.Units; }

                public static Pressure operator +(Pressure a, Pressure b)
                {
                    return (a.Units == b.Units) ? (new Pressure(a.Value + b.Value, a.Units)) : (a + b.ConvertToUnits(a.Units));
                }

                public static Pressure operator -(Pressure a, Pressure b)
                {
                    return (a.Units == b.Units) ? (new Pressure(a.Value - b.Value, a.Units)) : (a - b.ConvertToUnits(a.Units));
                }

                public int CompareTo(Pressure other)
                {
                    return (Units == other.Units) ? Value.CompareTo(other.Value) : this.CompareTo(other.ConvertToUnits(Units));
                }

                public bool Equals(Pressure other)
                {
                    return (other != null && ((Units == other.Units && Value == other.Value) || (Units != other.Units && this.Equals(other.ConvertToUnits(Units)))));
                }
            }

            /// <summary>Unit of measure helper type.  Represents a double value in PressureUnits.kilopascal.  implicit and explicit casts are used to convert between double and this UOM as well as between values with related UOMs</summary>
            public class KPa : Pressure
            {
                private const PressureUnits units = PressureUnits.kilopascals;

                public KPa(double value) : base(value, units) { }
                public KPa(Pressure fromPressure) : base(fromPressure, units) { }

                public static explicit operator KPa(double value) { return new KPa(value); }
            }

            /// <summary>Unit of measure helper type.  Represents a double value in PressureUnits.pascal.  implicit and explicit casts are used to convert between double and this UOM as well as between values with related UOMs</summary>
            public class Pascal : Pressure
            {
                private const PressureUnits units = PressureUnits.pascals;

                public Pascal(double value) : base(value, units) { }
                public Pascal(Pressure fromPressure) : base(fromPressure, units) { }

                public static explicit operator Pascal(double value) { return new Pascal(value); }
            }

            /// <summary>Unit of measure helper type.  Represents a double value in PressureUnits.torr.  implicit and explicit casts are used to convert between double and this UOM as well as between values with related UOMs</summary>
            public class Torr : Pressure
            {
                private const PressureUnits units = PressureUnits.torr;

                public Torr(double value) : base(value, units) { }
                public Torr(Pressure fromPressure) : base(fromPressure, units) { }

                public static explicit operator Torr(double value) { return new Torr(value); }
            }

            /// <summary>Unit of measure helper type.  Represents a double value in PressureUnits.millitor.  implicit and explicit casts are used to convert between double and this UOM as well as between values with related UOMs</summary>
            public class mTorr : Pressure
            {
                private const PressureUnits units = PressureUnits.millitorr;

                public mTorr(double value) : base(value, units) { }
                public mTorr(Pressure fromPressure) : base(fromPressure, units) { }

                public static explicit operator mTorr(double value) { return new mTorr(value); }
            }

            /// <summary>Unit of measure helper type.  Represents a double value in PressureUnits.bar.  implicit and explicit casts are used to convert between double and this UOM as well as between values with related UOMs</summary>
            public class Bar : Pressure
            {
                private const PressureUnits units = PressureUnits.bar;

                public Bar(double value) : base(value, units) { }
                public Bar(Pressure fromPressure) : base(fromPressure, units) { }

                public static explicit operator Bar(double value) { return new Bar(value); }
            }

            /// <summary>Unit of measure helper type.  Represents a double value in PressureUnits.millibar.  implicit and explicit casts are used to convert between double and this UOM as well as between values with related UOMs</summary>
            public class mBar : Pressure
            {
                private const PressureUnits units = PressureUnits.millibar;

                public mBar(double value) : base(value, units) { }
                public mBar(Pressure fromPressure) : base(fromPressure, units) { }

                public static explicit operator mBar(double value) { return new mBar(value); }
            }

            /// <summary>Unit of measure helper type.  Represents a double value in PressureUnits.psi.  implicit and explicit casts are used to convert between double and this UOM as well as between values with related UOMs</summary>
            public class PSI : Pressure
            {
                private const PressureUnits units = PressureUnits.psi;

                public PSI(double value) : base(value, units) { }
                public PSI(Pressure fromPressure) : base(fromPressure, units) { }

                public static explicit operator PSI(double value) { return new PSI(value); }
            }

            /// <summary>Unit of measure helper type.  Represents a double value in PressureUnits.StandardAtmospheres.  implicit and explicit casts are used to convert between double and this UOM as well as between values with related UOMs</summary>
            public class Atm : Pressure
            {
                public static Atm StandardValue { get { return new Atm(Constants.StdAtmInStdAtm); } }

                private const PressureUnits units = PressureUnits.StandardAtmospheres;

                public Atm(double value) : base(value, units) { }
                public Atm(Pressure fromPressure) : base(fromPressure, units) { }

                public static explicit operator Atm(double value) { return new Atm(value); }
            }

            /// <summary>Unit of measure helper type.  Represents a double value in PressureUnits.inchwc.  implicit and explicit casts are used to convert between double and this UOM as well as between values with related UOMs</summary>
            public class InchWC : Pressure
            {
                private const PressureUnits units = PressureUnits.inchwc;

                public InchWC(double value) : base(value, units) { }
                public InchWC(Pressure fromPressure) : base(fromPressure, units) { }

                public static explicit operator InchWC(double value) { return new InchWC(value); }
            }
        }
        
        #endregion

        #region Pressure (PressureUnits, Contants, ExtensionMethods - conversions)

        /// <summary>
        /// Pressure units of measure
        /// <para/>None (0), kilopascals (1), pascals (2), torr (3), millitorr (4), bar (5), millibar (6), psi (7), StandardAtmospheres (8)
        /// </summary>
        public enum PressureUnits : int
        {
            /// <summary>No Pressure Units of Measure have been provided (0) - placeholder</summary>
            None = 0,

            /// <summary>kilopascals (1)</summary>
            kilopascals = 1,

            /// <summary>pascals (2)</summary>
            pascals = 2,

            /// <summary>torr (3)</summary>
            torr = 3,

            /// <summary>millitor (4)</summary>
            millitorr = 4,

            /// <summary>bar (5)</summary>
            bar = 5,
            
            /// <summary>millibar (6)</summary>
            millibar = 6,
            
            /// <summary>Pounds per square inch (7)</summary>
            psi = 7,

            /// <summary>Standard Atmospheres (8)</summary>
            StandardAtmospheres = 8,

            /// <summary>Inch Water Column (9)</summary>
            inchwc = 9,
        }

        public static partial class Constants
        {
            /// <summary>1.0</summary>
            public const double StdAtmInStdAtm = 1.0;

            /// <summary>101.325</summary>
            public const double StdAtmInKPa = 101.325;

            /// <summary>1.01325</summary>
            public const double StdAtmInBar = 1.01325;

            /// <summary>760.0</summary>
            public const double StdAtmInTorr = 760.0;

            /// <summary>14.69595</summary>
            public const double StdAtmInPSI = 14.69595;

            /// <summary>0.249082</summary>
            public const double KPaPerInchWC = 0.249082;
        }

        public static partial class ExtensionMethods
        {
            /// <summary>
            /// Returns true if the given value is None
            /// </summary>
            public static bool IsNone(this PressureUnits value)
            {
                return (value == PressureUnits.None);
            }

            /// <summary>
            /// Converts the given valueInUserUnits from the given fromUOM PressureUnits into kilopascals and returns it.
            /// </summary>
            public static double ConvertToKPa(this PressureUnits fromUOM, double valueInUserUnits)
            {
                switch (fromUOM)
                {
                    case PressureUnits.psi: return valueInUserUnits * (Constants.StdAtmInKPa / Constants.StdAtmInPSI);
                    case PressureUnits.bar: return valueInUserUnits * (Constants.StdAtmInKPa / Constants.StdAtmInBar);
                    case PressureUnits.millibar: return valueInUserUnits * (0.001 * Constants.StdAtmInKPa / Constants.StdAtmInBar);
                    case PressureUnits.torr: return valueInUserUnits * (Constants.StdAtmInKPa / Constants.StdAtmInTorr);
                    case PressureUnits.millitorr: return valueInUserUnits * (0.001 * Constants.StdAtmInKPa / Constants.StdAtmInTorr);
                    case PressureUnits.pascals: return valueInUserUnits * 0.001;
                    case PressureUnits.kilopascals: return valueInUserUnits;
                    case PressureUnits.StandardAtmospheres: return valueInUserUnits * (Constants.StdAtmInKPa / Constants.StdAtmInStdAtm);
                    case PressureUnits.inchwc: return valueInUserUnits * Constants.KPaPerInchWC;
                    default: return 0.0;
                }
            }

            /// <summary>
            /// Converts the given valueInKPa in kilopascals into a value of the given toUOM PressureUnits and returns it.
            /// </summary>
            public static double ConvertFromKPa(this PressureUnits toUOM, double valueInKPa)
            {
                switch (toUOM)
                {
                    case PressureUnits.psi: return valueInKPa * (Constants.StdAtmInPSI / Constants.StdAtmInKPa);
                    case PressureUnits.bar: return valueInKPa * (Constants.StdAtmInBar / Constants.StdAtmInKPa);
                    case PressureUnits.millibar: return valueInKPa * (1000.0 * Constants.StdAtmInBar / Constants.StdAtmInKPa);
                    case PressureUnits.torr: return valueInKPa * (Constants.StdAtmInTorr / Constants.StdAtmInKPa);
                    case PressureUnits.millitorr: return valueInKPa * (1000.0 * Constants.StdAtmInTorr / Constants.StdAtmInKPa);
                    case PressureUnits.pascals: return valueInKPa * 1000.0;
                    case PressureUnits.kilopascals: return valueInKPa;
                    case PressureUnits.StandardAtmospheres: return valueInKPa * (Constants.StdAtmInStdAtm / Constants.StdAtmInKPa);
                    case PressureUnits.inchwc: return valueInKPa * (1.0 / Constants.KPaPerInchWC);
                    default:
                        return 0.0;
                }
            }

            [Obsolete("Please change to the use of the corresponding ConvertFromKPa extension method (2018-11-10)")]
            public static double ConvertFromKPA(this PressureUnits toUOM, double valueInKPa)
            {
                return toUOM.ConvertFromKPa(valueInKPa);
            }

            /// <summary>
            /// Converts the given <paramref name="value"/> measured in <paramref name="fromUOM"/> PressureUnits into a value of the given <paramref name="toUOM"/> PressureUnits and returns it.
            /// </summary>
            public static double ConvertUnits(this double value, PressureUnits fromUOM, PressureUnits toUOM)
            {
                if (fromUOM == toUOM)
                    return value;

                return toUOM.ConvertFromKPa(fromUOM.ConvertToKPa(value));
            }

            /// <summary>
            /// Converts the given <paramref name="value"/> measured in <paramref name="fromUOM"/> PressureUnits into a value of the given <paramref name="toUOM"/> PressureUnits and returns it.
            /// </summary>
            [Obsolete("Please replace use of this method with the use of the less ambiguous value.ConvertUnits method that has a consistent signature accross the different UOM values (2018-11-11)")]
            public static double ConvertUnits(this PressureUnits fromUOM, double value, PressureUnits toUOM)
            {
                return value.ConvertUnits(fromUOM, toUOM);
            }

            /// <summary>
            /// Converts the given <paramref name="value"/> measured in <paramref name="fromUOM"/> PressureUnits into a value of the given <paramref name="toUOM"/> PressureUnits and returns it.
            /// </summary>
            [Obsolete("Please replace use of this method with the use of the less ambiguous value.ConvertUnits method that has a consistent signature accross the different UOM values (2018-11-11)")]
            public static double ConvertUnits(this PressureUnits fromUOM, PressureUnits toUOM, double value)
            {
                return value.ConvertUnits(fromUOM, toUOM);
            }
        }

        #endregion

        #region VolumetricFlow (scms, sccm, slm)

        namespace VolumetricFlow
        {
            /// <summary>Base class for pressure Unit of measure helper types.</summary>
            public class VolumetricFlow : IComparable<VolumetricFlow>, IEquatable<VolumetricFlow>
            {
                public double Value { get; set; }
                public VolumetricFlowUnits Units { get; protected set; }
                public VolumetricFlow ConvertToUnits(VolumetricFlowUnits toUnits) { return new VolumetricFlow(this, toUnits); }

                public VolumetricFlow(VolumetricFlow other) : this(other.Value, other.Units) { }
                protected VolumetricFlow(double value, VolumetricFlowUnits units) { Value = value; Units = units; }
                protected VolumetricFlow(VolumetricFlow fromFlow, VolumetricFlowUnits units) : this(fromFlow.Value.ConvertUnits(fromFlow.Units, units), units) { }

                public override string ToString() { return "{0:g3} {1}".CheckedFormat(Value, Units); }

                public static explicit operator double(VolumetricFlow value) { return value.Value; }
                public static explicit operator VolumetricFlowUnits(VolumetricFlow value) { return value.Units; }

                public static VolumetricFlow operator +(VolumetricFlow a, VolumetricFlow b)
                {
                    return (a.Units == b.Units) ? (new VolumetricFlow(a.Value + b.Value, a.Units)) : (a + b.ConvertToUnits(a.Units));
                }

                public static VolumetricFlow operator -(VolumetricFlow a, VolumetricFlow b)
                {
                    return (a.Units == b.Units) ? (new VolumetricFlow(a.Value - b.Value, a.Units)) : (a - b.ConvertToUnits(a.Units));
                }

                public int CompareTo(VolumetricFlow other)
                {
                    return (Units == other.Units) ? Value.CompareTo(other.Value) : this.CompareTo(other.ConvertToUnits(Units));
                }

                public bool Equals(VolumetricFlow other)
                {
                    return (other != null && ((Units == other.Units && Value == other.Value) || (Units != other.Units && this.Equals(other.ConvertToUnits(Units)))));
                }
            }

            /// <summary>Unit of measure helper type.  Represents a double value in TemperatureUnits.scms.  implicit and explicit casts are used to convert between double and this UOM as well as between values with related UOMs</summary>
            public class scms : VolumetricFlow
            {
                private const VolumetricFlowUnits units = VolumetricFlowUnits.scms;

                public scms(double value) : base(value, units) { }
                public scms(VolumetricFlow fromFlow) : base(fromFlow, units) { }

                public static explicit operator scms(double value) { return new scms(value); }
            }

            /// <summary>Unit of measure helper type.  Represents a double value in TemperatureUnits.sccm.  implicit and explicit casts are used to convert between double and this UOM as well as between values with related UOMs</summary>
            public class sccm : VolumetricFlow
            {
                private const VolumetricFlowUnits units = VolumetricFlowUnits.sccm;

                public sccm(double value) : base(value, units) { }
                public sccm(VolumetricFlow fromFlow) : base(fromFlow, units) { }

                public static explicit operator sccm(double value) { return new sccm(value); }
            }

            /// <summary>Unit of measure helper type.  Represents a double value in TemperatureUnits.slm.  implicit and explicit casts are used to convert between double and this UOM as well as between values with related UOMs</summary>
            public class slm : VolumetricFlow
            {
                private const VolumetricFlowUnits units = VolumetricFlowUnits.slm;

                public slm(double value) : base(value, units) { }
                public slm(VolumetricFlow fromFlow) : base(fromFlow, units) { }

                public static explicit operator slm(double value) { return new slm(value); }
            }
        }
        
        #endregion

        #region VolumetricFlow (VolumetricFlowUnits, Contants, ExtensionMethods - conversions)

        /// <summary>
        /// Volumetrc units of flow at Standard Atmospheric pressure (1.0)
        /// <para/>None (0), scms (1), sccm (2), slm (3)
        /// </summary>
        public enum VolumetricFlowUnits : int
        {
            /// <summary>No Volumetric Units of Measure have been provided (0) - placeholder</summary>
            None = 0,

            /// <summary>standard cubic meters per second (1)</summary>
            scms = 1,

            /// <summary>standard cubic centemeters per minute (2)</summary>
            sccm = 2,

            /// <summary>standard litres per minute (3)</summary>
            slm = 3,
        }

        public static partial class ExtensionMethods
        {
            /// <summary>
            /// Returns true if the given value is None
            /// </summary>
            public static bool IsNone(this VolumetricFlowUnits value)
            {
                return (value == VolumetricFlowUnits.None);
            }

            /// <summary>
            /// Converts the given valueInUserUnits from the given fromUOM PressureUnits into kilopascals and returns it.
            /// </summary>
            public static double ConvertToSCMS(this VolumetricFlowUnits fromUOM, double valueInUserUnits)
            {
                switch (fromUOM)
                {
                    case VolumetricFlowUnits.scms: return valueInUserUnits;
                    case VolumetricFlowUnits.slm: return (valueInUserUnits / 60000.0); // 1000 l/cm, 60 s/m
                    case VolumetricFlowUnits.sccm: return (valueInUserUnits / 60000000.0); // 1000 l/cm, 1000 cc/l, 60 s/m
                    default: return 0.0;
                }
            }

            /// <summary>
            /// Converts the given valueInKPa in kilopascals into a value of the given toUOM PressureUnits and returns it.
            /// </summary>
            public static double ConvertFromSCMS(this VolumetricFlowUnits toUOM, double valueInSCMS)
            {
                switch (toUOM)
                {
                    case VolumetricFlowUnits.scms: return valueInSCMS;
                    case VolumetricFlowUnits.slm: return (valueInSCMS * 60000.0);   // 1000 l/cm, 60 s/m
                    case VolumetricFlowUnits.sccm: return (valueInSCMS * 60000000.0); // 1000 l/cm, 1000 cc/l, 60 s/m
                    default:
                        return 0.0;
                }
            }

            /// <summary>
            /// Converts the given <paramref name="value"/> measured in <paramref name="fromUOM"/> VolumetricFlowUnits into a value of the given <paramref name="toUOM"/> VolumetricFlowUnits and returns it.
            /// </summary>
            public static double ConvertUnits(this double value, VolumetricFlowUnits fromUOM, VolumetricFlowUnits toUOM)
            {
                if (fromUOM == toUOM)
                    return value;

                return toUOM.ConvertFromSCMS(fromUOM.ConvertToSCMS(value));
            }

            /// <summary>
            /// Converts the given <paramref name="value"/> measured in <paramref name="fromUOM"/> VolumetricFlowUnits into a value of the given <paramref name="toUOM"/> VolumetricFlowUnits and returns it.
            /// </summary>
            [Obsolete("Please replace use of this method with the use of the less ambiguous value.ConvertUnits method that has a consistent signature accross the different UOM values (2018-11-11)")]
            public static double ConvertUnits(this VolumetricFlowUnits fromUOM, double value, VolumetricFlowUnits toUOM)
            {
                return value.ConvertUnits(fromUOM, toUOM);
            }

            /// <summary>
            /// Converts the given <paramref name="value"/> measured in <paramref name="fromUOM"/> TemperatureUnits into a value of the given <paramref name="toUOM"/> PressureUnits and returns it.
            /// </summary>
            [Obsolete("Please replace use of this method with the use of the newer ConvertToUnits method that has a consistent signature accross the different UOM values (2018-11-11)")]
            public static double ConvertUnits(this VolumetricFlowUnits fromUOM, VolumetricFlowUnits toUOM, double value)
            {
                return value.ConvertUnits(fromUOM, toUOM);
            }
        }

        #endregion
    }
}
