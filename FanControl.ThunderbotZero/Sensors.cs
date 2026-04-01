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
        private float _lastSetValue = -1;

        public EcFanControl(string id, string name, byte writeRegister)
        {
            Id = id;
            Name = name;
            _writeRegister = writeRegister;
        }

        public string Id { get; }
        public string Name { get; }
        public float? Value { get; private set; }

        public void Update()
        {
            // No subprocess call — use last set value or 0
            if (_lastSetValue >= 0)
                Value = _lastSetValue;
        }

        public void Set(float val)
        {
            _lastSetValue = val;
            byte duty = (byte)System.Math.Max(0, System.Math.Min(100, val));
            EcAccess.WriteRegister(_writeRegister, duty);
        }

        public void Reset()
        {
            _lastSetValue = -1;
            EcAccess.WriteRegister(_writeRegister, EcAccess.AUTO_VALUE);
        }
    }
}
