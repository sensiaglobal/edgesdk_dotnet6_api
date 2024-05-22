using Sensia.HCC2.SDK.Classes;
using Newtonsoft.Json;

namespace Sensia.HCC2.SDK.Lib
{
    public class MiscFuncs
    {
        public static VarModel Readhcc2IOConfigFile(string vars_file_name)
        {
            string vars_json = File.ReadAllText(vars_file_name);
            Server server = new Server();
            Variables variables = JsonConvert.DeserializeObject<Variables>(vars_json);
            VarModel model = new VarModel(server, variables);
            return model;
        }
        public static AppData ReadAppConfigFile(string file_name)
        {
            string json = File.ReadAllText(file_name);
            AppData appData = JsonConvert.DeserializeObject<AppData>(json);
            return appData;
        }
        public static uint SwapWordBytes(uint val, bool ws, bool bs)
        {
            byte[] v = BitConverter.GetBytes(val);
            int a=0, b, c, d;
            
            if (!ws && !bs)
            {
                a=0; b=1; c=2; d=3;
            }
            else if (!ws && bs)
            {
                a=1; b=0; c=3; d=2;
            }
            else if (ws && !bs)
            {
                a=2; b=3; c=0; d=1;
            }
            else 
            {
                a=3; b=2; c=1; d=0;                
            }
            byte[] rtn2 = { v[a], v[b], v[c], v[d] };
            return BitConverter.ToUInt32(rtn2);
        }

        public static ulong SwapWordBytes(ulong val, bool ws, bool bs)
        {
            byte[] v = BitConverter.GetBytes(val);
            int a=0, b, c, d, e, f, g, h;
            
            if (!ws && !bs)
            {
                a=0; b=1; c=2; d=3; e=4; f=5; g=6; h=7;
            }
            else if (!ws && bs)
            {
                a=1; b=0; c=3; d=2; e=5; f=4; g=7; h=6;
            }
            else if (ws && !bs)
            {
                a=2; b=3; c=0; d=1; e=6; f=7; g=4; h=5;
            }
            else 
            {
                a=7; b=6; c=5; d=4; e=3; f=2; g=1; h=0;
            }
            byte[] rtn2 = { v[a], v[b], v[c], v[d], v[e], v[f], v[g], v[h] };
            return BitConverter.ToUInt64(rtn2);
        }


        public static uint[] SplitUlongToUintArr(ulong val)
        {
            byte[] v = BitConverter.GetBytes(val);
            
            byte[] rtn1 = { v[0], v[1], v[2], v[3] };
            byte[] rtn2 = { v[4], v[5], v[6], v[7] };

            uint uint1 = BitConverter.ToUInt32(rtn1);
            uint uint2 = BitConverter.ToUInt32(rtn2);

            uint[] rtn = { uint2, uint1 };

            return rtn;
        }



        public static T SetupParameter<T> (T value, T min)
        {
            if ((dynamic) value < (dynamic) min)
            {
                return min;
            }
            return value;
        }

        public static string GetEnvVariableWithDefault(string AEnvVariableName, string ADefault)
        {
            string envVar = Environment.GetEnvironmentVariable(AEnvVariableName);

            if (envVar != null)
                return envVar;

            return ADefault;
        }

        public static object ConvertUnsignedToSigned (object uvalue, int size)
        {
            object rtn;
            switch (size)
            {
                case sizeof(short):
                    short vs1 = (short) uvalue;
                    rtn = (object) vs1;
                    break;
                case sizeof(int):
                    int vs2 = (int) uvalue;
                    rtn = (object) vs2;
                    break;
                case sizeof(long):
                    long vs3 = (long) uvalue;
                    rtn = (object) vs3;
                    break;
                default:
                    throw new Exception("error. Value cannot be converrted. Size cannot be grater than " + sizeof(long).ToString() + ". Check configuration.");
            }
            return rtn;
        }
    }
}