using FanControl.Plugins;

namespace FanControl.ThunderbotZero
{
    public class ThunderbotZeroPlugin : IPlugin2
    {
        private EcTemperatureSensor _cpuTemp;
        private EcTemperatureSensor _gpuTemp;
        private EcFanSensor _fan1Sensor;
        private EcFanSensor _fan2Sensor;
        private EcFanControl _fan1Control;
        private EcFanControl _fan2Control;

        public string Name => "Thunderobot ZERO EC";

        public void Initialize()
        {
            _cpuTemp = new EcTemperatureSensor(
                "thunderobot/cpu_temp", "TR ZERO CPU Temp", EcAccess.REG_CPU_TEMP);

            _gpuTemp = new EcTemperatureSensor(
                "thunderobot/gpu_temp", "TR ZERO GPU Temp", EcAccess.REG_GPU_TEMP);

            _fan1Sensor = new EcFanSensor(
                "thunderobot/fan1_speed", "TR ZERO Fan1 (CPU)", EcAccess.REG_FAN1_READ);

            _fan2Sensor = new EcFanSensor(
                "thunderobot/fan2_speed", "TR ZERO Fan2 (GPU)", EcAccess.REG_FAN2_READ);

            _fan1Control = new EcFanControl(
                "thunderobot/fan1_ctrl", "TR ZERO Fan1 Control",
                EcAccess.REG_FAN1_WRITE, EcAccess.REG_FAN1_READ);

            _fan2Control = new EcFanControl(
                "thunderobot/fan2_ctrl", "TR ZERO Fan2 Control",
                EcAccess.REG_FAN2_WRITE, EcAccess.REG_FAN2_READ);
        }

        public void Load(IPluginSensorsContainer container)
        {
            container.TempSensors.Add(_cpuTemp);
            container.TempSensors.Add(_gpuTemp);
            container.FanSensors.Add(_fan1Sensor);
            container.FanSensors.Add(_fan2Sensor);
            container.ControlSensors.Add(_fan1Control);
            container.ControlSensors.Add(_fan2Control);
        }

        public void Update()
        {
            _cpuTemp?.Update();
            _gpuTemp?.Update();
            _fan1Sensor?.Update();
            _fan2Sensor?.Update();
            _fan1Control?.Update();
            _fan2Control?.Update();
        }

        public void Close()
        {
            // Restore auto mode on close
            _fan1Control?.Reset();
            _fan2Control?.Reset();
        }
    }
}
