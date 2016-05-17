﻿using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using System.IO.Ports;
using System.Threading;
using System.Diagnostics;

namespace DeviceIO
{
    public class SerialDevice : IDeviceIO, IDisposable
    {
        private SerialPort _COMPort;
        private string _returnToken;

        private bool _communicatyionIsActive = false;
        private int _dataCount;
        private string _dataReading;
        private ConcurrentQueue<string> _dataQueue;

        private double _PacketsRate;
        private Thread _serialThread;
        private Stopwatch _timeCounter;
        private const int _freqCriticalLimit = 20;

        public SerialDevice(string portName, int baudRate, Parity parity, int dataBits, StopBits stopBits, string returnToken = "\n")
        {
            _dataReading = string.Empty;
            _dataQueue = new ConcurrentQueue<string>();

            _timeCounter = new Stopwatch();
            _timeCounter.Start();

            _COMPort = new SerialPort(portName, baudRate, parity, dataBits, stopBits);

            _returnToken = returnToken;

            _COMPort.NewLine = _returnToken;
            _COMPort.RtsEnable = true;
            _COMPort.DtrEnable = true;

            _COMPort.ReadTimeout = SerialPort.InfiniteTimeout;
            _COMPort.WriteTimeout = SerialPort.InfiniteTimeout;

            _COMPort.Open();

            if (!_COMPort.IsOpen)
                _COMPort.Open();
            if (!_COMPort.IsOpen)
                throw new Exception("Can't connect to the COM port!");

            _serialThread = new Thread(new ThreadStart(GetSerialDataContinious));
            _serialThread.Priority = ThreadPriority.Normal;
            _serialThread.Name = string.Format("SerialHandle{0}", _serialThread.ManagedThreadId);

            _communicatyionIsActive = true;
            _serialThread.Start();
        }

        ~SerialDevice()
        {
            Dispose();
        }

        private int _getData(ref byte[] bytes, int offset, int count)
        {
            int readBytes = 0;
            if (count > 0)
                readBytes = _COMPort.Read(bytes, offset, count);

            return readBytes;
        }

        private void GetSerialDataContinious()
        {
            while (_communicatyionIsActive)
            {
                _timeCounter.Restart();

                _dataCount = _COMPort.BytesToRead;
                var buffer = new byte[_dataCount];
                var readBytes = _getData(ref buffer, 0, _dataCount);

                if (readBytes > 0)
                {
                    _dataReading += Encoding.ASCII.GetString(buffer);
                    if (_dataReading.Contains(_COMPort.NewLine))
                    {
                        _dataQueue.Enqueue(_dataReading);
                        _dataReading = string.Empty;
                    }
                }

                _PacketsRate = ((_PacketsRate + readBytes) / 2);

                var toSleep = (int)_timeCounter.ElapsedMilliseconds;
                if ((double)(readBytes + _COMPort.BytesToRead) / 2.0 <= _PacketsRate)
                    if (toSleep > 0)
                        Thread.Sleep(toSleep > _freqCriticalLimit ? _freqCriticalLimit : toSleep);
            }
        }

        public void SendCommandRequest(string request)
        {
            request = request.EndsWith("\n") ? request : request + "\n";

            var strBytes = Encoding.ASCII.GetBytes(request);
            _COMPort.Write(strBytes, 0, strBytes.Length);
        }

        public string ReceiveDeviceAnswer()
        {
            while (_dataQueue.Count == 0) ;

            string result;
            bool success = _dataQueue.TryDequeue(out result);
            if (success)
                return result;
            else
                throw new Exception("Unsuccessfull data reading!");
        }

        public string RequestQuery(string query)
        {
            SendCommandRequest(query);
            return ReceiveDeviceAnswer();
        }

        public void Dispose()
        {
            _communicatyionIsActive = false;
            while (_serialThread.IsAlive) ;

            if (_COMPort != null)
            {
                if (_COMPort.IsOpen)
                {
                    _COMPort.Close();
                    _COMPort.Dispose();
                }
                else
                    _COMPort.Dispose();
            }

            GC.SuppressFinalize(this);
        }
    }
}