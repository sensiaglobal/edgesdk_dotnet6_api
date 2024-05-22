using Sensia.HCC2.SDK.Lib;
using Sensia.HCC2.SDK.Classes;
using Microsoft.Extensions.Configuration;
using Serilog;
using NModbus;

namespace Sensia.hcc2sdkcs
{
    public class HCC2Interface
    {
        private IConfigurationRoot appConfig { get; set; }
        private bool comm_status { get; set; }
        private bool prev_status { get; set; }
        private Server hcc2ServerModel { get; set; }
        private Dictionary<RegisterTypeEnum, List<VariablesModel>> hcc2VariablesList { get; set; }

        private Dictionary<RegisterTypeEnum, List<ScanBucket>> hcc2BucketsDict { get; set; }

        private DB db { get; set; }
        private NModbusLib mbl { get; set; }
        private EventWaitHandle ready_ev { get; set; }   
   
        public HCC2Interface(IConfigurationRoot appConfig, DB db, EventWaitHandle ready_ev)
        {
            this.appConfig = appConfig;
            this.db = db;
            this.ready_ev = ready_ev;
            hcc2VariablesList = new Dictionary<RegisterTypeEnum, List<VariablesModel>>();
            hcc2BucketsDict = new Dictionary<RegisterTypeEnum, List<ScanBucket>>();
            this.comm_status = false;
            this.prev_status = false;            
        }
        public void StartClient()
        {
            string vars_file_name = "";
            int digital_bucket_size;
            int analog_bucket_size;
            int scan_ratio;
            int MINANALOGBUCKETSIZE = 10;
            int MINDIGITALBUCKETSIZE = 16;
            int MINTIMEBETWEENPOLLS = 100;
            int MINTIMEBETWEENCONTROLS = 100;
            int MINTIMEOUT = 100;
            //
            // Read variable configuration json file
            //
            try
            {
                Log.Information("Engine: Version: " + GenConfiguration.Version);
                vars_file_name = GenConfiguration.VarsFileName;
                digital_bucket_size = GenConfiguration.DigitalBucketSize;
                digital_bucket_size = MiscFuncs.SetupParameter<int>(digital_bucket_size, MINDIGITALBUCKETSIZE);
                analog_bucket_size = GenConfiguration.AnalogBucketsize;
                analog_bucket_size = MiscFuncs.SetupParameter<int>(analog_bucket_size, MINANALOGBUCKETSIZE);
                var hcc2Model = MiscFuncs.Readhcc2IOConfigFile(vars_file_name);

                hcc2ServerModel = hcc2Model.Server;
                //
                // Read Environmental variables
                //
                hcc2ServerModel.Host = MiscFuncs.GetEnvVariableWithDefault("HOST_NAME", hcc2ServerModel.Host);
                hcc2ServerModel.Port = Convert.ToInt32(MiscFuncs.GetEnvVariableWithDefault("PORT", hcc2ServerModel.Port.ToString()));
                hcc2ServerModel.SlaveId = Convert.ToByte(MiscFuncs.GetEnvVariableWithDefault("SLAVE_ID", hcc2ServerModel.SlaveId.ToString()));

                hcc2ServerModel.TimeBetweenPolls = Convert.ToInt32(MiscFuncs.GetEnvVariableWithDefault("TIME_BETWEEN_POLLS", hcc2ServerModel.TimeBetweenPolls.ToString()));
                hcc2ServerModel.TimeBetweenPolls = MiscFuncs.SetupParameter<int>(hcc2ServerModel.TimeBetweenPolls, MINTIMEBETWEENPOLLS);

                hcc2ServerModel.TimeBetweenControls = Convert.ToInt32(MiscFuncs.GetEnvVariableWithDefault("TIME_BETWEEN_CONTROLS", hcc2ServerModel.TimeBetweenControls.ToString()));
                hcc2ServerModel.TimeBetweenControls = MiscFuncs.SetupParameter<int>(hcc2ServerModel.TimeBetweenControls, MINTIMEBETWEENCONTROLS);

                if (hcc2ServerModel.TimeBetweenPolls < hcc2ServerModel.TimeBetweenControls)
                {
                    Log.Warning("Time Between polls: (" + hcc2ServerModel.TimeBetweenPolls.ToString() + ") cannot be lower than Time Between Controls: (" + hcc2ServerModel.TimeBetweenControls.ToString() + "). Setting TimeBetweenPolls = TimeBetweenControls.");
                    hcc2ServerModel.TimeBetweenPolls = hcc2ServerModel.TimeBetweenControls;
                }

                scan_ratio = hcc2ServerModel.TimeBetweenPolls/hcc2ServerModel.TimeBetweenControls;

                hcc2ServerModel.Timeout = Convert.ToInt32(MiscFuncs.GetEnvVariableWithDefault("TIMEOUT", hcc2ServerModel.Timeout.ToString()));
                hcc2ServerModel.Timeout = MiscFuncs.SetupParameter<int>(hcc2ServerModel.Timeout, MINTIMEOUT);

                var hcc2VariablesModel = hcc2Model.Variables;

                hcc2VariablesList.Add(RegisterTypeEnum.InputStatus, hcc2VariablesModel.Input_status);
                hcc2VariablesList.Add(RegisterTypeEnum.Coil, hcc2VariablesModel.Coils);
                hcc2VariablesList.Add(RegisterTypeEnum.InputRegister, hcc2VariablesModel.Input_registers);
                hcc2VariablesList.Add(RegisterTypeEnum.HoldingRegister, hcc2VariablesModel.Holding_registers);
            }
            catch (Exception e)
            {
                Log.Error("Unable to open vars configuration file: " + vars_file_name + ". Error: " + e.Message + ". Application Aborted.");
                return;
            }
            //
            // Variable Model was read. 
            // Defined Inputs and Outputs must be all read on each cycle.
            // Output should be updated (written) on change.
            //
            try
            {
                foreach (KeyValuePair<RegisterTypeEnum, List<VariablesModel>> vml in hcc2VariablesList)
                {   
                    int bucket_size = digital_bucket_size;

                    if ((vml.Key == RegisterTypeEnum.InputRegister) || (vml.Key == RegisterTypeEnum.HoldingRegister))
                    {
                        bucket_size = analog_bucket_size;
                    }
                    List<ScanBucket> buckets = Variables.DefineScanBuckets(vml.Value, bucket_size);
                    db.AddVariableListToDB(vml);
                    hcc2BucketsDict.Add(vml.Key, buckets);
                }
            }
            catch (Exception e)
            {
                Log.Error("Unable to create the buckets. Error: " + e.Message + ". Application Aborted.");
                return;
            }
            //
            // Try to connect with the Server 
            //
            MbsTcpClient client = new MbsTcpClient();

            int ratio_counter = 0;
            bool reported = false;           
            while (true)
            {                
                if (client.IsDisposed == true)
                {
                    client = new MbsTcpClient();
                }
                if (client.Connected == false)
                {
                    try
                    {
                        client.Connect(hcc2ServerModel.Host, hcc2ServerModel.Port);
                        var factory = new ModbusFactory();
                        IModbusMaster master = factory.CreateMaster(client);
                        master.Transport.ReadTimeout = hcc2ServerModel.Timeout;
                        master.Transport.WriteTimeout = hcc2ServerModel.Timeout;
                        master.Transport.Retries = hcc2ServerModel.Retries;
                        //
                        Log.Information("Connected with Server: " + hcc2ServerModel.Host + ", Port: " + hcc2ServerModel.Port.ToString() + ".");
                        reported = false;
                        //
                        // create the Modbus controller object
                        //
                        mbl = new NModbusLib(client, master);
                    }
                    catch (Exception e)
                    {
                        if (reported == false)
                        {
                            Log.Error("Unable to connect with server: " + hcc2ServerModel.Host + ", Port: " + hcc2ServerModel.Port.ToString() + ". Error: " + e.Message + ". Retrying...");
                            reported = true;
                        }
                        Thread.Sleep(hcc2ServerModel.TimeBetweenControls);
                        continue;
                    }
                }
                // let's check if there are any pending commands to issue
                comm_status = true;                   
                try
                {
                    IssuePendingControls(db, mbl, hcc2ServerModel);
                }
                catch (ArgumentOutOfRangeException eaoore)
                {
                    Log.Error("Argument Out Of range exception: " + hcc2ServerModel.Host + ", Method: IssuePendingControls. Error: " + eaoore.Message + ". Retrying...");
                    comm_status = false;
                }
                catch (IOException eio)
                {
                    Log.Error("I/O Exception trying to issue commands to server: " + hcc2ServerModel.Host + ", Method: IssuePendingControls. Error: " + eio.Message + ". Retrying...");
                    client.Close(); // close connection
                    client.Dispose(); // close port
                    Thread.Sleep(hcc2ServerModel.TimeBetweenControls);
                    comm_status = false;
                }
                catch (Exception e)
                {
                    Log.Error("Error trying to issue commands to server: " + hcc2ServerModel.Host + ", Port: " + hcc2ServerModel.Port.ToString() + ". Error: " + e.Message + ". Retrying...");
                    comm_status = false;
                }

                if (ratio_counter == 0)
                {
                    //
                    // Start scanning according to the buckets
                    //
                    foreach (KeyValuePair<RegisterTypeEnum, List<ScanBucket>> buckets in hcc2BucketsDict)
                    {
                        string scan_method = "";
                        comm_status = true;
                        try
                        {
                            foreach (ScanBucket bucket in buckets.Value)
                            {
                                switch (buckets.Key)
                                {
                                    case RegisterTypeEnum.InputStatus:
                                        scan_method = "ScanInputStatus";
                                        ScanInputStatus(db, mbl, hcc2ServerModel, bucket);
                                        break;
                                    case RegisterTypeEnum.Coil:
                                        scan_method = "ScanCoil";
                                        ScanCoil(db, mbl, hcc2ServerModel, bucket);
                                        break;
                                    case RegisterTypeEnum.InputRegister:
                                        scan_method = "ScanInputRegister";
                                        ScanInputRegister(db, mbl, hcc2ServerModel, bucket);
                                        break;
                                    case RegisterTypeEnum.HoldingRegister:
                                        scan_method = "ScanHoldingRegister";
                                        ScanHoldingRegister(db, mbl, hcc2ServerModel, bucket);
                                        break;
                                    case RegisterTypeEnum.Unknown:
                                    default:    
                                        Log.Error("Type of register is not allowed. Please check configuration.");
                                        break;
                                }
                            }
                        }
                        catch (IOException eio)
                        {
                            Log.Error("ModbusIO Library Exception in  server: " + hcc2ServerModel.Host + " Method: " + scan_method + ". Error: " + eio.Message + ". Restarting comms...");
                            client.Close(); // close connection
                            client.Dispose(); // close port
                            comm_status = false;
                            Thread.Sleep(hcc2ServerModel.TimeBetweenControls);
                            break;
                        }
                        catch (Exception e)
                        {
                            Log.Error("Unable to process data from server: " + hcc2ServerModel.Host + " Method: " + scan_method + ". Error: " + e.Message);
                            comm_status = false;
                            break;
                        }
                    }
                    //
                    // if comms were successful, fire up the app
                    //
                    if (comm_status == true)
                    {
                        ready_ev.Set();
                    }

                    //
                    // if communication failed for some reason, put the quality of all data in OLD
                    //
                    if (!comm_status && prev_status)
                    {
                        db.SetAllQuality(QualityEnum.old);
                    }
                    prev_status = comm_status;
                }
                //
                // Follow the scan/control ratio
                //
                ratio_counter++;
                if (ratio_counter == scan_ratio)
                {
                    ratio_counter = 0;
                }
                //
                // Sleep between controls 
                //
                Thread.Sleep(hcc2ServerModel.TimeBetweenControls);
            }
            Log.Information("Engine loop ended.");
        }

        private void IssuePendingControls(DB db, NModbusLib mbl, Server server)
        {
            //
            // go through db so check if there are any pending controls
            //
            List<VariablesModel> vml = db.GetAllPendingQueuedControlVars();
            //
            // issue those controls

            foreach (VariablesModel vm in vml)
            {
                RegisterTypeEnum rtype = RegisterType.ConvertStringToRegisterType(vm.Register_type);
                DTypeEnum dtype = DType.ConvertStringToType(vm.Type);
                RealtimeControl value = vm.Realtime_control.ReadRealtimeControl();

                // check register type
                if (rtype == RegisterTypeEnum.Coil)
                {
                    // issue the coil control  
                    mbl.WriteCoils(server.SlaveId, Convert.ToUInt16(vm.Register_number), Convert.ToBoolean(value.Value));
                }
                else if (rtype == RegisterTypeEnum.HoldingRegister)
                {
                    // issue control depending of number of registers
                    ushort s = 0;
                    if (vm.Num_registers == 1)
                    {
                        if (dtype == DTypeEnum.Type_float)
                        {
                            Log.Error ("Tag: " + vm.Name + " - Invalid data type. Number of registers: " + vm.Num_registers.ToString() + " is not allowed for a float. Write skipped.");
                            continue;
                        }
                        else if (dtype == DTypeEnum.Type_double)
                        {
                            Log.Error ("Tag: " + vm.Name + " - Invalid data type. Number of registers: " + vm.Num_registers.ToString() + " is not allowed for a double. Write skipped.");
                            continue;
                        }
                        else if (dtype == DTypeEnum.Type_integer)
                        {
                            s  = (ushort) (short) value.Value;
                        }
                        else 
                        {
                            s = (ushort) value.Value;
                        }
                        try
                        {
                            mbl.WriteHoldingRegisters(server.SlaveId, Convert.ToUInt16(vm.Register_number), s);
                        }
                        catch (Exception e)
                        {
                            Log.Error ("Tag: " + vm.Name + " - Error trying to write on unit: " + server.SlaveId.ToString() + ", reg number: " + vm.Register_number.ToString() + ", value: " + s.ToString() + ". Error: " + e.Message + ". Write skipped.");
                            continue;
                        }
                    }
                    else if (vm.Num_registers == 2)
                    {
                        uint ivalue = 0;
                        if (dtype == DTypeEnum.Type_double)
                        {
                            Log.Error ("Tag: " + vm.Name + " - Invalid data type. Number of registers: " + vm.Num_registers.ToString() + " is not allowed for a double. Write skipped.");
                            continue;
                        }
                        if (dtype == DTypeEnum.Type_float)
                        {
                            ivalue = (uint) BitConverter.SingleToInt32Bits((float) value.Value);
                        }
                        else if (dtype == DTypeEnum.Type_integer)
                        {
                            int v = (int) value.Value;
                            ivalue = (uint) v;
                        }
                        else 
                        {
                            ivalue = (uint) value.Value;
                        }
                        try
                        {
                            ivalue = MiscFuncs.SwapWordBytes(ivalue, vm.Word_swap, vm.Byte_swap);
                            mbl.WriteHoldingRegisters32(server.SlaveId, Convert.ToUInt16(vm.Register_number), Convert.ToUInt32(ivalue));
                        }
                        catch (Exception e)
                        {
                            Log.Error ("Tag: " + vm.Name + " - Error trying to write on unit: " + server.SlaveId.ToString() + ", reg number: " + vm.Register_number.ToString() + ", value: " + ivalue.ToString() + ". Error: " + e.Message + ". Write skipped.");
                            continue;
                        }
                    }
                    else if (vm.Num_registers == 4)
                    {
                        ulong ivalue = 0;
                        if (dtype == DTypeEnum.Type_float)
                        {
                            Log.Error ("Tag: " + vm.Name + " - Invalid data type. Number of registers: " + vm.Num_registers.ToString() + " is not allowed for a float. Write skipped.");
                            continue;
                        }
                        if (dtype == DTypeEnum.Type_double)
                        {
                            var test = value.Value.GetType();
                            ivalue = (ulong) BitConverter.DoubleToInt64Bits((double) value.Value);
                        }
                        else if (dtype == DTypeEnum.Type_integer)
                        {
                            long l = Convert.ToInt64(value.Value);
                            ivalue = (ulong) l;
                        }
                        else
                        {
                            ivalue = (ulong) value.Value;
                        }
                        try
                        {
                            ivalue = MiscFuncs.SwapWordBytes(ivalue, vm.Word_swap, vm.Byte_swap);
                            mbl.WriteMultipleRegisters32(server.SlaveId, Convert.ToUInt16(vm.Register_number), MiscFuncs.SplitUlongToUintArr(ivalue));
                        }
                        catch (Exception e)
                        {
                            Log.Error ("Tag: " + vm.Name + " - Error trying to write on unit: " + server.SlaveId.ToString() + ", reg number: " + vm.Register_number.ToString() + ", value: " + ivalue.ToString() + ". Error: " + e.Message + ". Write skipped.");
                            continue;
                        }
                    }
                    else 
                    {
                        Log.Error ("Tag: " + vm.Name + " - Invalid register type. Number of registers: " + vm.Num_registers.ToString() + " is not allowed. Point skipped.");
                        continue;
                    }
                }
                else 
                {
                    Log.Error ("Tag: " + vm.Name + " - Invalid register type. Register type: " + vm.Register_type.ToString() + " cannot be written. Point skipped.");
                    continue;
                }
                //
                // update database
                //
                db.ClearUpdateBitControlOnDB(vm.Name);
            }
        }

        private void ScanInputStatus(DB db, NModbusLib mbl, Server server, ScanBucket bucket)
        {
            ScanDigital(db, mbl, server, bucket, RegisterTypeEnum.InputStatus);
        }
        private void ScanDigital(DB db, NModbusLib mbl, Server server, ScanBucket bucket, RegisterTypeEnum reg_type)
        {
            bool[] data = null;
            if (reg_type == RegisterTypeEnum.InputStatus)
            {
                //
                // scan. If comm failure it will return Modbus.SlaveException
                //
                data = mbl.ReadInputStatus(server.SlaveId, Convert.ToUInt16(bucket.StartRegister), Convert.ToUInt16(bucket.NumRegisters));
            }
            else
            {
                data = mbl.ReadCoils(server.SlaveId, Convert.ToUInt16(bucket.StartRegister), Convert.ToUInt16(bucket.NumRegisters));
            }
            //
            // try to place each register to its corresponding place in DB
            //
            foreach (string tagName in bucket.tagNames)
            {
                object ovalue = null;
                bool bvalue = false;

                VariablesModel vrm =  db.GetVariablesModelFromDB(tagName);

                if (vrm == null) 
                {
                    throw new Exception("tagname: " + tagName + " does not exist yet in DB.");
                }
                
                for (int i = vrm.Register_number; i<vrm.Register_number + vrm.Num_registers; i++)
                {
                    bvalue = data[i - bucket.StartRegister];
                }
                //
                // save to DB
                //
                ovalue = bvalue;
                if (db.UpdateRealTimeDataOnDB(tagName, ovalue, DType.ConvertStringToType(vrm.Type), QualityEnum.ok) == false)
                {
                    throw new Exception("tagname: " + tagName + " does not exist yet in DB.");
                }
            }
            return;
        }
        private void ScanCoil(DB db, NModbusLib mbl, Server server, ScanBucket bucket)
        {
            ScanDigital(db, mbl, server, bucket, RegisterTypeEnum.Coil);
        }
        private void ScanInputRegister(DB db, NModbusLib mbl, Server server, ScanBucket bucket)
        {
            ScanAnalogRegister(db, mbl, server, bucket, RegisterTypeEnum.InputRegister);
        }
        private void ScanHoldingRegister(DB db, NModbusLib mbl, Server server, ScanBucket bucket)
        {
            ScanAnalogRegister(db, mbl, server, bucket, RegisterTypeEnum.HoldingRegister);
        }

        private void ScanAnalogRegister(DB db, NModbusLib mbl, Server server, ScanBucket bucket, RegisterTypeEnum reg_type)
        {
            ushort[] data = null;

            if (reg_type == RegisterTypeEnum.InputRegister)
            {
                //
                // scan. If comm failure it will return Modbus.SlaveException
                //
                data = mbl.ReadInputRegisters(server.SlaveId, Convert.ToUInt16(bucket.StartRegister), Convert.ToUInt16(bucket.NumRegisters));
                //
                // try to place each register to its corresponding place in DB
                //
            }
            else if (reg_type == RegisterTypeEnum.HoldingRegister) 
            {
                //
                // scan. If comm failure it will return Modbus.SlaveException
                //
                data = mbl.ReadHoldingRegisters(server.SlaveId, Convert.ToUInt16(bucket.StartRegister), Convert.ToUInt16(bucket.NumRegisters));
            }
            else
            {
                throw new Exception("Invalid register type on call. Review configuration.");
            }
            //
            // try to place each register to its corresponding place in DB
            //
            foreach (string tagName in bucket.tagNames)
            {
                object ovalue = null;
                ushort ushrtval = 0;
                uint ivalue = 0;
                ulong lvalue = 0;
                float fvalue = 0.0f;
                double dvalue = 0.0;

                VariablesModel vrm =  db.GetVariablesModelFromDB(tagName);
                if (vrm == null) 
                {
                    throw new Exception("tagname: " + tagName + " does not exist yet in DB.");
                }
                
                List<Object> value_array = new List<object>();

                DTypeEnum type = DType.ConvertStringToType(vrm.Type);

                for (int element=0; element < vrm.Collection_size; element++)
                {
                    int j = 0;
                    var initialRegister = vrm.Register_number + (element * vrm.Num_registers);

                    for (int i = initialRegister; i<initialRegister + vrm.Num_registers; i++)
                    {
                        ushrtval = data[i - bucket.StartRegister];
                        if (vrm.Word_swap == false)
                        {
                            ivalue = (ivalue << 16) + data[i - bucket.StartRegister];
                            lvalue = (lvalue << 16) + data[i - bucket.StartRegister];
                        }
                        else
                        {                    
                            ivalue += Convert.ToUInt32(data[i- bucket.StartRegister]<< (16*j));
                            lvalue += Convert.ToUInt64(data[i- bucket.StartRegister]) << (16*j);
                        }
                        j++;
                    }
                    //
                    // convert to the appropriate type
                    //
                    if (type == DTypeEnum.Type_float)
                    {
                        //
                        // Convert integer to float
                        //
                        fvalue = BitConverter.UInt32BitsToSingle(ivalue);
                        //
                        // save to DB
                        //
                        ovalue = fvalue;
                    }
                    else if (type == DTypeEnum.Type_double)
                    {
                        //
                        // Convert integer to float
                        //
                        dvalue = BitConverter.UInt64BitsToDouble(lvalue);
                        //
                        // save to DB
                        //
                        ovalue = dvalue;
                    }
                    else if (type == DTypeEnum.Type_integer)
                    {
                        if (vrm.Num_registers == 1)
                        {
                            ovalue = (object) (Int16) ivalue; 
                        }
                        else
                        {
                            ovalue = (object) (int) ivalue; // convert uint to int (32-bit)
                        }
                    }
                    else
                    {
                        if (vrm.Num_registers == 1)
                        {
                            ovalue = (object) (UInt16) ivalue; 
                        }
                        else 
                        {
                            ovalue = (object) ivalue;
                        }                       
                    }

                    value_array.Add(ovalue);
                }
                if (value_array.Count == 1)
                {
                    if (db.UpdateRealTimeDataOnDB(tagName, value_array[0], type, QualityEnum.ok) == false)
                    {
                        throw new Exception("tagname: " + tagName + " does not exist yet in DB.");
                    }
                }
                else
                {
                    if (db.UpdateRealTimeDataOnDB(tagName, value_array, type, QualityEnum.ok) == false)
                    {
                        throw new Exception("tagname: " + tagName + " does not exist yet in DB.");
                    }
                }
            }
            return;
        }    
    }
}