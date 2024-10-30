using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BusinessLayer.Models.WeldingMachine
{
    public class PanelState
    {
        public DateTime LastDatetimeUpdate { get; set; }
        public ICollection<PanelLedItem> Leds { get; set; }
        public ICollection<PanelTextItem> Texts { get; set; }

        public ICollection<SummaryProperty> SummaryProperties { get; set; }

        public PanelWorkerInfo WorkerInfo { get; set; }

        public double IReal { get; set; }
        public double UReal { get; set; }

        public PanelState()
        {
            Leds = new List<PanelLedItem>();
            Texts = new List<PanelTextItem>();
        }
    }

    public class PanelWorkerInfo
    {
        public int UserAccountID { get; set; }
        public string Name { get; set; }
        public string Photo { get; set; }
    }

    public class PanelItem
    {
        public double X { get; set; }
        public double Y { get; set; }
        public string Color { get; set; }
    }

    public class PanelLedItem : PanelItem
    {
        public double Width { get; set; }
        public double Height { get; set; }
        public double Radius { get; set; }
    }

    public class PanelTextItem : PanelItem
    {
        public string Text { get; set; }
        public double FontSize { get; set; }
        public string FontFamily { get; set; }
        public string FontStyle { get; set; }
    }

    public class SummaryProperty
    {
        public string PropertyCode { get; set; }
        public string Title { get; set; }
        public string Value { get; set; }
        public string Unit { get; set; }
        public string PropertyType { get; set; }
    }
}
