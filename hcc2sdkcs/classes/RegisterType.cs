namespace Sensia.HCC2.SDK.Classes
{
    public class RegisterType 
    {
        static string INPUTSTATUS = "input_status";
        static string COIL = "coil";
        static string INPUTREGISTER = "input_register";
        static string HOLDINGREGISTER = "holding_register";
        static string UNKNOWN = "unknown";
        static Dictionary<string, RegisterTypeEnum> RegisterMap = new Dictionary<string, RegisterTypeEnum> 
        {
            { INPUTSTATUS, RegisterTypeEnum.InputStatus },
            { COIL, RegisterTypeEnum.Coil },
            { INPUTREGISTER, RegisterTypeEnum.InputRegister },
            { HOLDINGREGISTER, RegisterTypeEnum.HoldingRegister }
        };



        public static RegisterTypeEnum ConvertStringToRegisterType(string type)
        {
            return RegisterMap[type];
        }

        public static string ConvertRegisterTypeToString(RegisterTypeEnum reg)
        {
            switch (reg)
            {
                case (RegisterTypeEnum.InputStatus):
                    return INPUTSTATUS;
                    
                case (RegisterTypeEnum.Coil):
                    return COIL;
                
                case (RegisterTypeEnum.InputRegister):
                    return INPUTREGISTER;
                    
                case (RegisterTypeEnum.HoldingRegister):
                    return HOLDINGREGISTER;
                    
                case (RegisterTypeEnum.Unknown):
                default:
                    return UNKNOWN;
                    
            }
        }
    }

    public enum RegisterTypeEnum
    {
        InputStatus = 1,
        Coil = 2,
        InputRegister = 3,
        HoldingRegister = 4,
        Unknown = -1
    }
}
