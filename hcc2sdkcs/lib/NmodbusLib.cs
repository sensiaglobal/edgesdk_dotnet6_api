using Sensia.HCC2.SDK.Classes;
using NModbus;
using NModbus.Extensions.Enron;

namespace Sensia.HCC2.SDK.Lib 
{
    public class NModbusLib
    {
        private MbsTcpClient client;
        private  IModbusMaster master;

        public NModbusLib(MbsTcpClient client, IModbusMaster master)
        {
            this.client = client;
            this.master = master;
        }
        public ushort[] ReadInputRegisters(byte slaveId, ushort startAddress, ushort numInputs)
        {    
            return master.ReadInputRegisters(slaveId, startAddress, numInputs);
        }
        public ushort[] ReadHoldingRegisters(byte slaveId, ushort startAddress, ushort numInputs)
        {    
            return master.ReadHoldingRegisters(slaveId, startAddress, numInputs);
        }
        public bool[] ReadInputStatus(byte slaveId, ushort startAddress, ushort numInputs)
        {
            return master.ReadInputs(slaveId, startAddress, numInputs);
        }
        public bool[] ReadCoils(byte slaveId, ushort startAddress, ushort numInputs)
        {
            return master.ReadCoils(slaveId, startAddress, numInputs);
        }
        public void WriteCoils(byte slaveId, ushort startAddress, bool value)
        {
            master.WriteSingleCoil(slaveId, startAddress, value);
        }
        public void WriteHoldingRegisters(byte slaveId, ushort startAddress, ushort value)
        {
            master.WriteSingleRegister(slaveId, startAddress, value);
        }
        public void WriteHoldingRegisters32(byte slaveId, ushort startAddress, uint value)
        {
            master.WriteSingleRegister32(slaveId, startAddress, value);
        }

        public void WriteMultipleRegisters32(byte slaveId, ushort startAddress, uint[] value)
        {
            master.WriteMultipleRegisters32(slaveId, startAddress, value);
        }

    }
}