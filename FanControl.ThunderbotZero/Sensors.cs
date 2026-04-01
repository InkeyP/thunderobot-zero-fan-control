using FanControl.Plugins;

namespace FanControl.ThunderbotZero
{
    internal class WmiTemperatureSensor : IPluginSensor
    {
        private readonly bool _isCpu;

        public WmiTemperatureSensor(string id, string name, bool isCpu)
        {
            Id = id;
            Name = name;
            _isCpu = isCpu;
        }

        public string Id { get; }
        public string Name { get; }
        public float? Value { get; private set; }

        public void Update()
        {
            var info = EcAccess.GetHardwareInfoCached();
            if (info.Valid)
            {
                int temp = _isCpu ? info.CpuTemp : info.GpuTemp;
                if (temp > 0 && temp < 150)
                    Value = temp;
            }
        }
    }

    internal class WmiFanSensor : IPluginSensor
    {
        private readonly bool _isCpu;

        public WmiFanSensor(string id, string name, bool isCpu)
        {
            Id = id;
            Name = name;
            _isCpu = isCpu;
        }

        public string Id { get; }
        public string Name { get; }
        public float? Value { get; private set; }

        public void Update()
        {
            var info = EcAccess.GetHardwareInfoCached();
            if (info.Valid)
            {
                int rpm = _isCpu ? info.CpuFanRpm : info.GpuFanRpm;
                if (rpm >= 0)
                    Value = rpm;
            }
        }
    }

    internal class EcFanControl : IPluginControlSensor
    {
        private readonly byte _writeRegister;
        private readonly byte _readRegister;

        public EcFanControl(string id, string name, byte writeRegister, byte readRegister)
        {
            Id = id;
            Name = name;
            _writeRegister = writeRegister;
            _readRegister = readRegister;
        }

        public string Id { get; }
        public string Name { get; }
        public float? Value { get; private set; }

        public void Update()
        {
            var val = EcAccess.ReadRegister(_readRegister);
            if (val.HasValue && val.Value >= 0 && val.Value <= 100)
                Value = val.Value;
        }

        public void Set(float val)
        {
            byte duty = (byte)System.Math.Max(0, System.Math.Min(100, val));
            EcAccess.WriteRegister(_writeRegister, duty);
        }

        public void Reset()
        {
            EcAccess.WriteRegister(_writeRegister, EcAccess.AUTO_VALUE);
        }
    }
}
