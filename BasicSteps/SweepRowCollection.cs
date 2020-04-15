namespace OpenTap.Plugins.BasicSteps
{
    public class SweepRowCollection : VirtualCollection<SweepRow>
    {
        SweepLoop2 loop; 
        public SweepLoop2 Loop
        {
            get => loop;
            set
            {
                loop = value;
                foreach (var item in this)
                    item.Loop = loop;
            }
        }

        public override void Add(SweepRow item)
        {
            item.Loop = Loop;
            base.Add(item);
        }

        public override void Insert(int index, SweepRow item)
        {
            item.Loop = Loop;
            base.Insert(index, item);
        }

        public override SweepRow this[int index]
        {
            get => base[index];
            set
            {
                value.Loop = Loop;
                base[index] = value;
            }
        }
    }
}