
using Sensia.HCC2.SDK.Config;

namespace Sensia.HCC2.SDK.Classes
{
    public class VarModel
    {
        public Server Server { get; set; }
        public Variables Variables { get; set; }

        public VarModel (Server server, Variables variables)
        {
            this.Server = server;
            this.Variables = variables;
        }

    }
    public class Server
    {
        public string Host { get; set; }
        public int Port { get; set; }
        public byte SlaveId {get; set; }
        public int TimeBetweenPolls { get; set; }
        public int TimeBetweenControls { get; set; }
        public int Timeout { get; set; }
        public int Retries { get; set; }

        public Server()
        {
            this.Host = ServerConfig.Host;
            this.Port = ServerConfig.Port;
            this.SlaveId = ServerConfig.SlaveID;
            this.TimeBetweenPolls = ServerConfig.TimeBetweenPolls;
            this.TimeBetweenControls = ServerConfig.TimeBetweenControls;
            this.Timeout = ServerConfig.Timeout;
            this.Retries = ServerConfig.Retries;
        }
    }
 
    public class Variables
    {
        public List<VariablesModel> Input_status { get; set; }
        public List<VariablesModel> Coils { get; set; }
        public List<VariablesModel> Input_registers { get; set; }
        public List<VariablesModel> Holding_registers { get; set; }

        public static List <ScanBucket> DefineScanBuckets(List<VariablesModel> list, int bucket_size)
        {
            List<ScanBucket> rtn = new List<ScanBucket>();
            if (list.Count > 0)
            {
                //
                // Go through the whole list 
                // Determine the distance between var registers
                //
                int start = -1;
                int size = -1;
                List<string> tagNames = new List<string>();

                HashSet<string> checkDuplicates = new  HashSet<string>();

                list.Sort(); // make sure all variables are in ascending order according to register number

                foreach (VariablesModel vm in list)
                {
                    if (checkDuplicates.Add(vm.Name) == false)
                    {
                        throw new Exception("duplicate variable name found: " + vm.Name );
                    }
                    if (start < 0)
                    {
                        start = vm.Register_number;
                        size = vm.Num_registers * vm.Collection_size;
                        tagNames.Add(vm.Name);
                        continue;
                    }
                    if ((vm.Register_number - start) <= bucket_size)
                    {
                        size = vm.Register_number - start + vm.Num_registers * vm.Collection_size;
                        tagNames.Add(vm.Name);
                    }
                    else
                    {
                        ScanBucket b = new ScanBucket(start, size);
                        b.AddTagsToBucket(tagNames);

                        rtn.Add(b);
                        start = vm.Register_number;
                        size = vm.Num_registers * vm.Collection_size;
                        tagNames = new List<string> { vm.Name };
                    }
                }
                ScanBucket bucket = new ScanBucket(start, size);
                bucket.AddTagsToBucket(tagNames);
                rtn.Add(bucket);
                //
                // Check all buckets for possible overlaps
                //
                int i = 0;
                foreach (ScanBucket a in rtn)
                {
                    int j = 0;
                    foreach (ScanBucket b in rtn) 
                    {
                        if (i != j) 
                        {
                            if (a == b)
                            {
                                throw new Exception("Register overlapping was found. Application aborted");
                            }
                        }
                        j++;
                    }
                    i++;
                }
            }
            return rtn;
        }

        private static bool DetectDuplicateStartRegister(int start, List<ScanBucket> rtn)
        {
            foreach (ScanBucket sb in rtn)
            {
                if (sb.StartRegister == start)
                {
                    return true;
                }
            }
            return false;
        }
    }

    public class VariablesModel : IComparable<VariablesModel> 
    {
        public string Name { get; set; }
        public string Type { get; set; }
        public int Collection_size {get; set;}
        public string Register_type { get; set; }
        public int Register_number { get; set; }
        public int Num_registers { get; set; }
        public bool Writable { get; set; } 
        public bool Word_swap { get; set; }
        public bool Byte_swap { get; set; }
        public RealtimeData Realtime_data { get; set; }
        public RealtimeControl Realtime_control { get; set; }
        public VariablesModel()
        {
            this.Realtime_data = new RealtimeData();
            this.Realtime_control = new RealtimeControl();
        }
        public int CompareTo(VariablesModel other)
        {
            return Register_number.CompareTo(other.Register_number);
        }
    }

    public class RealtimeControl : RealtimeData
    {
        public bool ControlPending;

        public RealtimeControl(): base()
        {
            ControlPending = false;
        }
        public RealtimeControl ReadRealtimeControl()
        {
            return this;
        }
        public void UpdateRealtimeControl(object value, DTypeEnum type, QualityEnum quality)
        {
            base.UpdateRealtimeData(value, type, quality);
            this.ControlPending = true;
        }
        public void UpdateControlQueueToDB(Queue<KeyValuePair<string, RealtimeControl>> controlQueue, string tagName)
        {
            controlQueue.Enqueue(new KeyValuePair<string, RealtimeControl>(tagName, ReadRealtimeControl()));
        }
    }
    public class RealtimeData 
    {
        public object Value { get; set; }
        public DTypeEnum Type {get; set; }
        public DateTime Timestamp { get; set; }
        public QualityEnum Quality { get; set; }

        public RealtimeData()
        {
            this.Type = DTypeEnum.Type_unknown;
            this.Quality = QualityEnum.Unknown;
        }
        public void UpdateRealtimeData(object value, DTypeEnum type, QualityEnum quality)
        {
            this.Value = value;
            this.Type = type;
            this.Timestamp = DateTime.UtcNow;
            this.Quality = quality;
        }

        public RealtimeData ReadRealtimeData()
        {
            return this;
        }
        public override string ToString()
        {
            return "Value: " + this.Value + ", TimeStamp: " + this.Timestamp + ", quality: " + this.Quality;
        }
    }

    public enum DTypeEnum
    {
        Type_boolean = 1,
        Type_integer = 2,
        Type_unsigned = 3,
        Type_float = 4,
        Type_double = 5,
        Type_unknown = -1
    }

    public class DType 
    {
        static readonly string TYPE_BOOLEAN = "boolean";
        static readonly string TYPE_INTEGER = "integer";
        static readonly string TYPE_UNSIGNED = "unsigned";
        static readonly string TYPE_FLOAT = "float";
        static readonly string TYPE_DOUBLE = "double";
        static readonly string TYPE_UNKNOWN = "unknown";
        static Dictionary<string, DTypeEnum> TypeMap = new Dictionary<string, DTypeEnum> 
        {
            { TYPE_BOOLEAN, DTypeEnum.Type_boolean },
            { TYPE_INTEGER, DTypeEnum.Type_integer },
            { TYPE_UNSIGNED, DTypeEnum.Type_unsigned },
            { TYPE_FLOAT, DTypeEnum.Type_float },
            { TYPE_DOUBLE, DTypeEnum.Type_double },
            { TYPE_UNKNOWN, DTypeEnum.Type_unknown }
        };
 
        public static DTypeEnum ConvertStringToType(string type)
        {
            return TypeMap[type];
        }

        public static string ConvertTypeToString(DTypeEnum reg)
        {
            switch (reg)
            {
                case (DTypeEnum.Type_boolean):
                    return TYPE_BOOLEAN;
                    
                case (DTypeEnum.Type_integer):
                    return TYPE_INTEGER;
                
                case (DTypeEnum.Type_unsigned):
                    return TYPE_UNSIGNED;

                case (DTypeEnum.Type_float):
                    return TYPE_FLOAT;
                    
                case (DTypeEnum.Type_double):
                    return TYPE_DOUBLE;
                    
                case (DTypeEnum.Type_unknown):
                default:
                    return TYPE_UNKNOWN; 
            }
        }
    }
}

