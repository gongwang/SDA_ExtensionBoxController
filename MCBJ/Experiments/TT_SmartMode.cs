﻿using ExperimentController;
using MotionManager;
using SourceMeterUnit;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MCBJ.Experiments
{
    public class TT_SmartMode : ExperimentBase
    {
        private ISourceMeterUnit smu;
        private IMotionController1D motor;

        private readonly double ConductanceQuantum = 0.0000774809173;

        public TT_SmartMode(ISourceMeterUnit SMU, IMotionController1D Motor)
        {
            smu = SMU;
            motor = Motor;
        }

        public override void ToDo(object Arg)
        {
            var settings = (TT_SmartModeInfo)Arg;

            var minPos = settings.MinPos;
            var maxPos = settings.MaxPos;
            var zeroPos = settings.ZeroPos;
            var setPos = settings.SetPos;
            var destPos = settings.DestPos;
            var nCycles = settings.nCycles;
            var ppm = settings.PointsPerMilimeter;

            var minConductance = settings.MinConductance;
            var maxConductance = settings.MaxConductance;

            var currPos = setPos;

            var currCycle = 1;

            var dz = 1.0 / (double)ppm;

            motor.Position = setPos;

            while (currCycle <= nCycles)
            {
                if (!IsRunning)
                    break;

                while (true)
                {
                    var scaledConductance = (1.0 / smu.Resistance) / ConductanceQuantum;

                    onDataArrived(new ExpDataArrivedEventArgs(string.Format("{0}\t{1}\r\n", currPos.ToString(NumberFormatInfo.InvariantInfo), scaledConductance.ToString(NumberFormatInfo.InvariantInfo))));

                    if (((scaledConductance < minConductance) && (currPos <= zeroPos)) ||
                        ((scaledConductance > minConductance) && (currPos >= maxPos)))
                    {
                        motor.Position = setPos;
                        IsRunning = false;
                        break;
                    }

                    if (currPos > destPos)
                    {
                        if (scaledConductance > maxConductance)
                        {
                            destPos = maxPos;
                            break;
                        }

                        currPos -= dz;
                        motor.Position = currPos;
                    }
                    else
                    {
                        if (scaledConductance < minConductance)
                        {
                            destPos = minPos;
                            break;
                        }

                        currPos += dz;
                        motor.Position = currPos;
                    }
                }

                currCycle += 1;
            }

            motor.Position = setPos;
        }
    }
}
