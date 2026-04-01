using FanControl.Plugins;

namespace FanControl.ThunderbotZero
{
    internal class EcTemperatureSensor : IPluginSensor
    {
        private readonly byte _register;

        public EcTemperatureSensor(string id, string name, byte register)
        {
            Id = id;
            Name = name;
            _register = register;
        }

        public string Id { get; }
        public string Name { get; }
        public float? Value { get; private set; }

        public void Update()
        {
            var val = EcAccess.ReadRegister(_register);
            if (val.HasValue && val.Value > 0 && val.Value < 150)
                Value = val.Value;
        }
    }

    internal class EcFanSensor : IPluginSensor
    {
        private readonly byte _register;

        public EcFanSensor(string id, string name, byte register)
        {
            Id = id;
            Name = name;
            _register = register;
        }

        public string Id { get; }
        public string Name { get; }
        public float? Value { get; private set; }

        public void Update()
        {
            var val = EcAccess.ReadRegister(_register);
            if (val.HasValue && val.Value >= 0 && val.Value <= 100)
                Value = val.Value;
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
