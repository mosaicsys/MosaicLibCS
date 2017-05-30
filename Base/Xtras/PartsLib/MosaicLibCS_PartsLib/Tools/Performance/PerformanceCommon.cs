//-------------------------------------------------------------------
/*! @file PerformanceCommon.cs
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
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using MosaicLib.Modular;
using MosaicLib.Modular.Action;
using MosaicLib.Modular.Common;
using MosaicLib.Modular.Config;
using MosaicLib.Modular.Config.Attributes;
using MosaicLib.Modular.Interconnect.Values;
using MosaicLib.Modular.Part;
using MosaicLib.Time;
using MosaicLib.Utils;

namespace MosaicLib.PartsLib.Tools.Performance
{
    /// <summary>
    /// This is an initial, general purpose, histograming tool for use with double values.
    /// Client defines the set of boundary values which divide up the bins.  
    /// For a client provided set of m boundary values there are m+1 bins.
    /// Bins are arranged so that the bin[i] contains items that are in the range from boundary[i-1] exclusive to boundary[i] inclusive 
    /// using -Infinity and +Infinity for the boundary values when i is off the corresonding end of the boundary array.
    /// The first bin includes all values that are up to and including the first boundary value while the last bin (m+1 th) includes all items 
    /// that are above the last boundary value.
    /// It is the reponsability of the client to make certain that the boundary values are listed in monotonic increasing order.
    /// <para/>This object calculates and can produce basic statistics about the data it is given including Min, Max, Average and Variance.
    /// It also includes a Median value estimator that uses the histogram contents to find the bin which spans half the count and then
    /// interpolates accross that bin's span based on the fraction of the count in that bin.
    /// </summary>
    public class Histogram
    {
        public Histogram(IEnumerable<double> binBoundarySet)
        {
            binBoundaryArray = binBoundarySet.SafeToArray();
            if (binBoundaryArray.IsEmpty())
                binBoundaryArray = new double[] { 0.0 };

            numBins = binBoundaryArray.Length + 1;
            binCountArray = new int[NumBins];
        }

        public Histogram(Histogram other)
        {
            numBins = other.numBins;
            binBoundaryArray = other.binBoundaryArray.MakeCopyOf();
            binCountArray = other.binCountArray.MakeCopyOf();

            Minimum = other.Minimum;
            Maximum = other.Maximum;
            Count = other.Count;
            valueSum = other.valueSum;
            valueSqSum = other.valueSqSum;
        }

        public void Add(Histogram other)
        {
            if (other == null || numBins != other.numBins || !binBoundaryArray.IsEqualTo(other.binBoundaryArray))
                return;

            if (Count == 0)
            {
                Minimum = other.Minimum;
                Maximum = other.Maximum;
                valueSum = other.valueSum;
                valueSqSum = other.valueSqSum;
                Count = other.Count;
            }
            else
            {
                Minimum = Math.Min(Minimum, other.Minimum);
                Maximum = Math.Max(Maximum, other.Maximum);
                valueSum += other.valueSum;
                valueSqSum += other.valueSqSum;
                Count += other.Count;
            }

            for (int idx = 0; idx < numBins; idx++)
            {
                binCountArray[idx] += other.binCountArray[idx];
            }
        }

        public void Add(double value)
        {
            double value2 = value * value;

            if (Count == 0)
            {
                Minimum = Maximum = value;
                valueSum = value;
                valueSqSum = value2;

                Count = 1;
            }
            else
            {
                if (Minimum > value)
                    Minimum = value;
                if (Maximum < value)
                    Maximum = value;

                valueSum += value;
                valueSqSum += value2;

                Count++;
            }

            int rangeLowestBinIndex = 0;
            int rangeHighestBinIndex = numBins - 1;

            for (; ; )
            {
                int testIndex = ((rangeLowestBinIndex + rangeHighestBinIndex) >> 1);
                double testIndexLowerBoundary = binBoundaryArray.SafeAccess(testIndex - 1, Double.NegativeInfinity);
                double testIndexUpperBoundary = binBoundaryArray.SafeAccess(testIndex, Double.PositiveInfinity);

                bool moveDown = (value <= testIndexLowerBoundary);
                bool moveUp = (value > testIndexUpperBoundary);

                if (moveDown)
                    rangeHighestBinIndex = (testIndex != rangeHighestBinIndex ? testIndex : rangeHighestBinIndex - 1);
                else if (moveUp)
                    rangeLowestBinIndex = (testIndex != rangeLowestBinIndex ? testIndex : rangeLowestBinIndex + 1);
                else
                {
                    binCountArray[testIndex]++;
                    return;
                }
            }
        }

        /// <summary>
        /// Gives the Minimum value that has been added to this histogram, or zero if the histogram is empty
        /// </summary>
        public double Minimum { get; private set; }

        /// <summary>
        /// Gives the Maximum value that has been added to this histogram, or zero if the histogram is empty
        /// </summary>
        public double Maximum { get; private set; }

        /// <summary>
        /// Gives the Average of the values that have been added to this histogram, or zero if the histogram is empty
        /// </summary>
        public double Average { get { return (Count > 0) ? (valueSum / Count) : 0.0; } }
        
        /// <summary>
        /// Gives the unbiased Variance of the values that have been added to this histogram, or zero if the histogram has had fewer than 2 values added to it.
        /// <para/>Based on the use of the Bessel's correction version of the Variance calculation where each value is interpreted as an observation
        /// <para/>Variance = ((valueSqSum - (valueSum * valueSum) / Count) / (Count - 1));
        /// </summary>
        public double Variance { get { return ((Count >= 2) ? ((valueSqSum - (valueSum * valueSum) / Count) / (Count - 1)) : 0.0); } }

        /// <summary>
        /// Gives the unbiased Standard Deviation of the values that have been added to this histogram, or zero if the histogram has had fewer than 2 values added to it.
        /// <para/>Based on the use of the Bessel's correction version of the Variance calculation where each value is interpreted as an observation
        /// <para/>Variance = ((valueSqSum - (valueSum * valueSum) / Count) / (Count - 1));
        /// <para/>StandardDeviation = sqrt(Variance);
        /// </summary>
        public double StandardDeviation { get { return Math.Sqrt(Variance); } }

        /// <summary>
        /// Gives the Sum of the values that have been added to the histogram, or zero if the histogram is empty.
        /// </summary>
        public double Sum { get { return valueSum; } set { valueSum = value; } }

        private double valueSum, valueSqSum;

        /// <summary>
        /// Gives the Count of the number of values that have been added to the histogram, or zero if the histogram is empty.
        /// </summary>
        public int Count { get; private set; }

        /// <summary>
        /// Gives access to the array of double's that define the boundary positions between the count bins.  This array is one shorter than the BinCountArray is.
        /// </summary>
        public double[] BinBoundaryArray { get { return binBoundaryArray; } }

        /// <summary>
        /// Gives access to the array of bin count values.  The values in the BinBoundaryArray give the value magnitudes that delinate these count bins.
        /// (The end of bin 0 is the boundary 0 value, etc.)
        /// </summary>
        public int[] BinCountArray { get { return binCountArray; } }

        /// <summary>
        /// Returns the number of bins in the BinCountArray (aka the BinCountArray Length or one more than the BinBoundaryArray Length)
        /// </summary>
        public int NumBins { get { return numBins; } }

        /// <summary>
        /// Returns a ValueContainer enclosed version of the BinBoundaryArray
        /// </summary>
        public ValueContainer BinBoundaryArrayVC { get { return new ValueContainer(binBoundaryArray); } }

        /// <summary>
        /// Returns a ValueContainer enclosed version of the BinCountArray
        /// </summary>
        public ValueContainer BinCountArrayVC { get { return new ValueContainer(binCountArray); } }

        private int numBins;
        private double[] binBoundaryArray;
        private int[] binCountArray;

        /// <summary>
        /// Clears the added values from the histogram.
        /// <para/>supports call chaining.
        /// </summary>
        /// <returns></returns>
        public Histogram Clear()
        {
            Count = 0;
            Minimum = Maximum = 0.0;
            valueSum = valueSqSum = 0.0;

            BinCountArray.SetAll(0);

            return this;
        }

        /// <summary>
        /// Debug and logging helper method
        /// </summary>
        public override string ToString()
        {
            return ToString(TSInclude.All);
        }

        [Flags]
        public enum TSInclude : int
        {
            Count = 0x01,
            Min = 0x02,
            Max = 0x04,
            Avg = 0x08,
            MedEst = 0x10,
            Var = 0x20,
            SD = 0x40,
            BinCountArray = 0x40,
            BinBoundaryArray = 0x80,

            BaseWithAvg = (Count | Min | Max | Avg),
            BaseWithMedEst = (Count | Min | Max | MedEst),
            BaseWithAvgAndMedEst = (Count | Min | Max | Avg | MedEst),
            All = (Count | Min | Max | Avg | MedEst | SD | BinCountArray | BinBoundaryArray),
        }

        /// <summary>
        /// Debug and logging helper method.  Caller provides the TSInclude flags value to define what properties to include in the ToString output.
        /// </summary>
        public string ToString(TSInclude tsInclude)
        {
            StringBuilder sb = new StringBuilder();

            String prefix = "";

            if (tsInclude.IsSet(TSInclude.Count)) { sb.CheckedAppendFormat("{0}Count:{1}", prefix, Count); prefix = " "; }
            if (tsInclude.IsSet(TSInclude.Min)){ sb.CheckedAppendFormat("{0}Min:{1:f6}", prefix, Minimum); prefix = " "; }
            if (tsInclude.IsSet(TSInclude.Max)){ sb.CheckedAppendFormat("{0}Max:{1:f6}", prefix, Maximum); prefix = " "; }
            if (tsInclude.IsSet(TSInclude.Avg)){ sb.CheckedAppendFormat("{0}Avg:{1:f6}", prefix, Average); prefix = " "; }
            if (tsInclude.IsSet(TSInclude.MedEst)){ sb.CheckedAppendFormat("{0}MedEst:{1:f6}", prefix, MedianEstimate); prefix = " "; }
            if (tsInclude.IsSet(TSInclude.Var)) { sb.CheckedAppendFormat("{0}Var:{1:e4}", prefix, Variance); prefix = " "; }
            if (tsInclude.IsSet(TSInclude.SD)) { sb.CheckedAppendFormat("{0}SD:{1:e4}", prefix, StandardDeviation); prefix = " "; }
            if (tsInclude.IsSet(TSInclude.BinCountArray)) { sb.CheckedAppendFormat("{0}{1}", prefix, BinCountArrayVC); prefix = (tsInclude.IsSet(TSInclude.BinCountArray | TSInclude.BinBoundaryArray)) ? " <= " : " "; }
            if (tsInclude.IsSet(TSInclude.BinBoundaryArray)) { sb.CheckedAppendFormat("{0}{1}", prefix, BinBoundaryArrayVC); prefix = " "; }

            return sb.ToString();
        }

        /// <summary>
        /// Computes and returns an estimate of the Median of the values that have been added to the histogram, or zero if the histogram is empty.
        /// Attempts to find the counter bin that that contains the "middle" value (rounded up) then estimates how far from low to high
        /// in the bin this value occurred, assuming that the added values in this bin were evenly distributed from low to high.
        /// </summary>
        public double MedianEstimate
        {
            get 
            {
                return GetPercentialValueEstimate(50.0);
            }
        }

        /// <summary>
        /// Computes and returns an estimate of the given <paramref name="percentile"/> of the values that have been added to the histogram, 
        /// or zero if the histogram is empty.
        /// Attempts to find the counter bin that that contains the given value and then estimates how far from low to high
        /// in the bin this value occurred, assuming that the added values in this bin were evenly distributed from low to high.
        /// If the first or last bin contains the percentile then this simply returns the position of the known edge of that bin (innermost edge).
        /// </summary>
        public double GetPercentialValueEstimate(double percentile)
        {
            if (Count == 0)
                return 0.0;

            double percentileAsCount = Count * percentile * 0.01;

            int index = 0;
            int priorBinCountSum = 0, currentBinCount = 0;
            double priorBinBoundary = Double.NegativeInfinity, currentBinBoundary = 0.0;

            for (; index < NumBins; index++)
            {
                currentBinCount = binCountArray[index];
                currentBinBoundary = binBoundaryArray.SafeAccess(index, Double.PositiveInfinity);

                if (percentileAsCount <= priorBinCountSum + currentBinCount)
                    break;

                priorBinCountSum += currentBinCount;
                priorBinBoundary = currentBinBoundary;
            }

            if (Double.IsNegativeInfinity(priorBinBoundary))
                return binBoundaryArray[0];

            if (Double.IsPositiveInfinity(currentBinBoundary))
                return binBoundaryArray[numBins - 2];

            double unitSpan = ((currentBinCount > 0) ? (double)(percentileAsCount - priorBinCountSum) / currentBinCount : 1.0);

            double estValue = unitSpan * (currentBinBoundary - priorBinBoundary) + priorBinBoundary;

            return estValue;
        }
    }

    internal class MDRFHistogramGroupSource
    {
        public MDRFHistogramGroupSource(string groupName, Histogram histogram, ulong fileIndexUserRowFlagBits = 0, INamedValueSet extraClientNVS = null, IEnumerable<PartsLib.Tools.MDRF.Writer.GroupPointInfo> extraGPISet = null)
        {
            GroupInfo = new MDRF.Writer.GroupInfo()
            {
                Name = groupName,
                GroupBehaviorOptions = MDRF.Writer.GroupBehaviorOptions.UseVCHasBeenSetForTouched | MDRF.Writer.GroupBehaviorOptions.IncrSeqNumOnTouched,
                FileIndexUserRowFlagBits = fileIndexUserRowFlagBits,
                GroupPointInfoArray = new[] { countGPI, minGPI, maxGPI, avgGPI, sdGPI, medianEstGPI, percentile5EstGPI, percentile95EstGPI, binsGPI }.Concat(extraGPISet ?? emptyGPIArray).ToArray(),
                ClientNVS = new NamedValueSet() { { "Histogram" }, { "NumBins", histogram.NumBins }, { "BinBoundaryArray", histogram.BinBoundaryArray } }.MergeWith(extraClientNVS ?? NamedValueSet.Empty, NamedValueMergeBehavior.AddNewItems).MakeReadOnly(),
            };

            Histogram = histogram;
            lastBinCountArray = Histogram.BinCountArray.MakeCopyOf();

            UpdateGroupItems();
            GroupInfo.Touched = true;
        }

        private PartsLib.Tools.MDRF.Writer.GroupPointInfo[] emptyGPIArray = new MDRF.Writer.GroupPointInfo[0];

        public Histogram Histogram { get; private set; }
        int[] lastBinCountArray;

        public void UpdateGroupItems()
        {
            countGPI.VC = countGPI.VC.SetValue<int>(Histogram.Count, countGPI.ValueCST, false);
            minGPI.VC = minGPI.VC.SetValue<double>(Histogram.Minimum, minGPI.ValueCST, false);
            maxGPI.VC = maxGPI.VC.SetValue<double>(Histogram.Maximum, maxGPI.ValueCST, false);
            avgGPI.VC = avgGPI.VC.SetValue<double>(Histogram.Average, avgGPI.ValueCST, false);
            sdGPI.VC = sdGPI.VC.SetValue<double>(Histogram.StandardDeviation, sdGPI.ValueCST, false);
            medianEstGPI.VC = medianEstGPI.VC.SetValue<double>(Histogram.MedianEstimate, medianEstGPI.ValueCST, false);
            percentile5EstGPI.VC = percentile5EstGPI.VC.SetValue<double>(Histogram.GetPercentialValueEstimate(5.0), percentile5EstGPI.ValueCST, false);
            percentile95EstGPI.VC = percentile95EstGPI.VC.SetValue<double>(Histogram.GetPercentialValueEstimate(95.0), percentile95EstGPI.ValueCST, false);

            if (!lastBinCountArray.Equals(Histogram.BinCountArray))
                binsGPI.VC = new ValueContainer(lastBinCountArray = Histogram.BinCountArray.MakeCopyOf());
        }

        public INamedValueSet AsNVS
        {
            get
            {
                return new NamedValueSet()
                {
                    { "NumBins",  Histogram.NumBins },
                    { "count", Histogram.Count },
                    { "min", Histogram.Minimum },
                    { "max", Histogram.Maximum },
                    { "avg", Histogram.Average },
                    { "sd", Histogram.StandardDeviation },
                    { "medianEst", Histogram.MedianEstimate },
                    { "percentile5Est", Histogram.GetPercentialValueEstimate(5.0) },
                    { "percentile95Est", Histogram.GetPercentialValueEstimate(95.0) },

                    { "BinBoundaryArray", Histogram.BinBoundaryArrayVC },
                    { "bins", Histogram.BinCountArrayVC },
                }.MakeReadOnly();
            }
        }

        public MDRF.Writer.GroupInfo GroupInfo { get; private set; }

        public MDRF.Writer.GroupPointInfo countGPI = new MDRF.Writer.GroupPointInfo() { Name = "count", Comment = "sample count", ValueCST = ContainerStorageType.Int32, VC = new ValueContainer(0) };
        public MDRF.Writer.GroupPointInfo minGPI = new MDRF.Writer.GroupPointInfo() { Name = "min", ValueCST = ContainerStorageType.Double, VC = new ValueContainer(0.0) };
        public MDRF.Writer.GroupPointInfo maxGPI = new MDRF.Writer.GroupPointInfo() { Name = "max", ValueCST = ContainerStorageType.Double, VC = new ValueContainer(0.0) };
        public MDRF.Writer.GroupPointInfo avgGPI = new MDRF.Writer.GroupPointInfo() { Name = "avg", ValueCST = ContainerStorageType.Double, VC = new ValueContainer(0.0) };
        public MDRF.Writer.GroupPointInfo sdGPI = new MDRF.Writer.GroupPointInfo() { Name = "sd", ValueCST = ContainerStorageType.Double, VC = new ValueContainer(0.0) };
        public MDRF.Writer.GroupPointInfo medianEstGPI = new MDRF.Writer.GroupPointInfo() { Name = "medianEst", ValueCST = ContainerStorageType.Double, VC = new ValueContainer(0.0) };
        public MDRF.Writer.GroupPointInfo percentile5EstGPI = new MDRF.Writer.GroupPointInfo() { Name = "percentile5Est", ValueCST = ContainerStorageType.Double, VC = new ValueContainer(0.0) };
        public MDRF.Writer.GroupPointInfo percentile95EstGPI = new MDRF.Writer.GroupPointInfo() { Name = "percentile95Est", ValueCST = ContainerStorageType.Double, VC = new ValueContainer(0.0) };
        public MDRF.Writer.GroupPointInfo binsGPI = new MDRF.Writer.GroupPointInfo() { Name = "bins" };

        public override string ToString()
        {
            if (Histogram != null)
                return "{0} {1}".CheckedFormat(GroupInfo.Name, Histogram);
            else
                return "{0} Empty".CheckedFormat(GroupInfo.Name);
        }
    }
}

//-------------------------------------------------------------------
