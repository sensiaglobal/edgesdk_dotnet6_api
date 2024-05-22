namespace Sensia.HCC2.SDK.Classes
{
    public class ScanBucket : IComparable<ScanBucket>
    {
        public int StartRegister { get; set; }
        public int NumRegisters { get; set; }
        public List<string> tagNames {get; set; }

        public ScanBucket (int start, int size)
        {
            this.tagNames = new List<string>();
            this.StartRegister = start;
            this.NumRegisters = size;
        }

        public void AddTagsToBucket(List<string> tagNames)
        {
            foreach (string tagName in tagNames)
            {
                if (this.tagNames.Contains(tagName) == false)
                {
                    this.tagNames.Add(tagName);
                }
            }
        }

        public int CompareTo(ScanBucket other)
        {
            int end = this.StartRegister + this.NumRegisters;
            int other_end = other.StartRegister + other.NumRegisters;

            if ((this.StartRegister <= other_end) && (other.StartRegister <= end))
            {
                return 1;
            }
            return 0;
        }
    }
}


