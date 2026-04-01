using FanControl.Plugins;

namespace FanControl.ThunderbotZero
{
    internal class WmiTemperatureSensor : IPluginSensor
    {
        private readonly bool _isCpu;
        public WmiTemperatureSensor(string id, string name, bool isCpu)
        { Id = id; Name = name; _isCpu = isCpu; }

        public string Id { get; }
        public string Name { get; }
        public float? Value { get; private set; }

        public void Update()
        {
            var info = EcAccess.GetHardwareInfoCached();
            if (info.Valid)
                Value = _isCpu ? info.CpuTemp : info.GpuTemp;
        }
    }

    internal class WmiFanSensor : IPluginSensor
    {
        private readonly bool _isCpu;
        public WmiFanSensor(string id, string name, bool isCpu)
        { Id = id; Name = name; _isCpu = isCpu; }

        public string Id { get; }
        public string Name { get; }
        public float? Value { get; private set; }

        public void Update()
        {
            var info = EcAccess.GetHardwareInfoCached();
            if (info.Valid)
                Value = _isCpu ? info.CpuFanRpm : info.GpuFanRpm;
        }
    }

    /// <summary>
    /// Fan control via direct EC register writes (0x2C/0x2D).
    /// WMI SetFanSpeed doesn't persist (EC overrides), but EC registers do.
    /// ec-probe is only called on Set/Reset (user action), never in Update loop.
    /// </summary>
    internal class EcFanControl : IPluginControlSensor
    {
        private readonly byte _register; // 0x2C or 0x2D
        private float _lastSet = -1;

        public EcFanControl(string id, string name, byte register)
        { Id = id; Name = name; _register = register; }

        public string Id { get; }
        public string Name { get; }
        public float? Value { get; private set; }

        public void Update()
        {
            if (_lastSet >= 0)
                Value = _lastSet;
        }

        public void Set(float val)
        {
            _lastSet = val;
            byte duty = (byte)System.Math.Max(0, System.Math.Min(100, val));
            EcAccess.WriteEcRegister(_register, duty);
        }

        public void Reset()
        {
            _lastSet = -1;
            EcAccess.WriteEcRegister(_register, 0xFF);
        }
    }
}
