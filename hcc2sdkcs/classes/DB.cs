
namespace Sensia.HCC2.SDK.Classes
{
    public class DB 
    {
        public Dictionary<string, VariablesModel> matrix;
        private Mutex db_mtx;
        private Queue<KeyValuePair<string, RealtimeControl>> controlQueue;

        public DB(Queue<KeyValuePair<string, RealtimeControl>> controlQueue)
        {
            matrix = new Dictionary<string, VariablesModel>();
            this.controlQueue = controlQueue;
            db_mtx = new Mutex(false);
        }

        public void AddVariableListToDB(KeyValuePair<RegisterTypeEnum, List<VariablesModel>> kvpair)
        {
            db_mtx.WaitOne();
            foreach (VariablesModel vrm in kvpair.Value)
            {
                vrm.Register_type = RegisterType.ConvertRegisterTypeToString(kvpair.Key);
                matrix.Add(vrm.Name, vrm);
            }
            db_mtx.ReleaseMutex();
        }

        public VariablesModel GetVariablesModelFromDB(string tagName)
        {
            db_mtx.WaitOne();
            VariablesModel rtn = null;
            if (matrix.ContainsKey(tagName) == true)
            {
                rtn = matrix[tagName];
            }
            
            db_mtx.ReleaseMutex();
            return rtn;
        }
        public RealtimeData GetValue(string tagName)
        {
            db_mtx.WaitOne();
            RealtimeData rtn = null;
            if (matrix.ContainsKey(tagName) == true)
            {
                rtn = matrix[tagName].Realtime_data;
            }
            db_mtx.ReleaseMutex();
            return rtn;
        }
        public bool SetValue(string tagName, object value, QualityEnum quality)
        {
            bool rtn = false;
            db_mtx.WaitOne();
            if (matrix.ContainsKey(tagName) == true)
            {
                DTypeEnum dtype = matrix[tagName].Realtime_data.Type;
                matrix[tagName].Realtime_control.UpdateRealtimeControl(value, dtype, QualityEnum.ok);
                matrix[tagName].Realtime_control.UpdateControlQueueToDB(this.controlQueue, tagName);
                rtn = true;
            }
            db_mtx.ReleaseMutex();
            return rtn;
        }

        public bool UpdateRealTimeDataOnDB(string tagName, object value, DTypeEnum type, QualityEnum quality)
        {
            db_mtx.WaitOne();
            bool rtn = false;
            if (matrix.ContainsKey(tagName) == true)
            {
                matrix[tagName].Realtime_data.UpdateRealtimeData(value, type, quality);
                rtn = true;
            }
            db_mtx.ReleaseMutex();
            return rtn;
        }
        public RealtimeData ReadRealTimeDataOnDB(string tagName)
        {
            db_mtx.WaitOne();
            RealtimeData rtn = null;
            if (matrix.ContainsKey(tagName) == true)
            {
                rtn = matrix[tagName].Realtime_data.ReadRealtimeData();
            }
            db_mtx.ReleaseMutex();
            return rtn;
        }

        public bool ClearUpdateBitControlOnDB(string tagName)
        {
            db_mtx.WaitOne();
            bool rtn = false;
            if (matrix.ContainsKey(tagName) == true)
            {
                matrix[tagName].Realtime_control.ControlPending=false;
                matrix[tagName].Realtime_control.Timestamp = DateTime.UtcNow;
                rtn = true;
            }
            db_mtx.ReleaseMutex();
            return rtn;
        }

        public List<VariablesModel> GetAllPendingControlVars() 
        {
            db_mtx.WaitOne();
            List<VariablesModel> rtn = new List<VariablesModel>();
            foreach (KeyValuePair<string, VariablesModel> vml in matrix)
            {
                if (vml.Value.Writable == true)
                {
                    if (vml.Value.Realtime_control.ControlPending == true)
                    {
                        rtn.Add(vml.Value);
                    }
                }
            }
            db_mtx.ReleaseMutex();
            return rtn;
        }  

    public List<VariablesModel> GetAllPendingQueuedControlVars()
    {
        db_mtx.WaitOne();
        List<VariablesModel> rtn = new List<VariablesModel>();
        while (true)
        {
            try
            {
                /// Check for pending controls to avoid exception
                if (controlQueue.Count == 0)
                {
                    break;
                }
                KeyValuePair<string, RealtimeControl>kvp = controlQueue.Dequeue();
                rtn.Add(matrix[kvp.Key]);
            }
            catch (InvalidOperationException)
            {
                break;
            }
        }
        db_mtx.ReleaseMutex();
        return rtn;
    }


        public List <VariablesModel> GetAllVariablesFromDB()
        {
            db_mtx.WaitOne();
            List <VariablesModel> rtn = matrix.Values.ToList();
            db_mtx.ReleaseMutex();
            return rtn;
        }

        public void SetAllQuality(QualityEnum quality) 
        {
            db_mtx.WaitOne();
            foreach (KeyValuePair<string, VariablesModel> vml in matrix)
            {
                vml.Value.Realtime_data.Quality = quality;
            }
            db_mtx.ReleaseMutex();
        }  
    }
}