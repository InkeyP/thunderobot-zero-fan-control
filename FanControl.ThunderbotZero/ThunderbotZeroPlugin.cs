using FanControl.Plugins;

namespace FanControl.ThunderbotZero
{
    public class ThunderbotZeroPlugin : IPlugin2
    {
        private WmiTemperatureSensor _cpuTemp, _gpuTemp;
        private WmiFanSensor _fan1Sensor, _fan2Sensor;
        private EcFanControl _fan1Control, _fan2Control;

        public string Name => "Thunderobot ZERO EC";

        public void Initialize()
        {
            _cpuTemp    = new WmiTemperatureSensor("thunderobot/cpu_temp", "TR ZERO CPU Temp", true);
            _gpuTemp    = new WmiTemperatureSensor("thunderobot/gpu_temp", "TR ZERO GPU Temp", false);
            _fan1Sensor = new WmiFanSensor("thunderobot/fan1_rpm", "TR ZERO Fan1 (CPU)", true);
            _fan2Sensor = new WmiFanSensor("thunderobot/fan2_rpm", "TR ZERO Fan2 (GPU)", false);
            _fan1Control = new EcFanControl("thunderobot/fan1_ctrl", "TR ZERO Fan1 Control", 0x2C);
            _fan2Control = new EcFanControl("thunderobot/fan2_ctrl", "TR ZERO Fan2 Control", 0x2D);
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
            _fan1Control?.Reset();
            _fan2Control?.Reset();
        }
    }
}
