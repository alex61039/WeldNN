using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BusinessLayer.Models.Configuration
{
    public class WeldingMachineTypeConfiguration
    {
        public Settings Settings { get; set; }
        public PropertyLimits PropertyLimits { get; set; }
        public MessageConfiguration Inbound { get; set; }
        public MessageConfiguration Outbound { get; set; }
        public PresentationConfiguration Presentation { get; set; }
        public ModeDefinitions ModeDefinitions { get; set; }
        public ICollection<AlertDefinition> AlertDefinitions { get; set; }
    }

    public class PropertyLimits
    {
        public ICollection<PropertyLimit> Limits { get; set; }
    }

    public class PropertyLimit
    {
        public string PropertyCode { get; set; }
        public string Description { get; set; }
        public double MinValue { get; set; }
        public double MaxValue { get; set; }
        public double Step { get; set; } = 1;
    }

    public class Settings
    {
        /// <summary>
        /// Hours.
        /// Total hours of working before service maintenance.
        /// </summary>
        public int WorkingTimeBeforeService { get; set; }

        /// <summary>
        /// Hours.
        /// Array of hour when to send Notification before next Maintenance/Service
        /// </summary>
        public ICollection<int> NotifyHoursBeforeService { get; set; }

        /// <summary>
        /// Days.
        /// Notify after N days since last service, or StartUsing, or DateCreated
        /// </summary>
        public int NotifyDaysSinceService { get; set; }

        /// <summary>
        /// КПД аппарата в режиме сварки. Значение от 0 до 1.
        /// </summary>
        public double Efficiency { get; set; }

        /// <summary>
        /// Потребляемая мощность аппарата при простое, в Ваттах.
        /// </summary>
        public double StandbyPower { get; set; }
    }

    public class MessageConfiguration {
        public string Delimiter { get; set; }

        /// <summary>
        /// Bytes/Chars
        /// </summary>
        public int DataPropertiesOffset { get; set; }

        public ICollection<DataProperty> Header { get; set; }
        public ICollection<DataProperty> Body { get; set; }

        public MessageConfiguration()
        {
            Header = new List<DataProperty>();
            Body = new List<DataProperty>();
        }
    }


    public class DataProperty
    {
        /// <summary>
        /// Relative to the PropertiesOffset.
        /// First property always has Start=0.
        /// Bytes/Chars.
        /// </summary>
        public int Start { get; set; }

        /// <summary>
        /// Bytes/Chars
        /// </summary>
        public int Length { get; set; }

        /// <summary>
        /// Must contain a constant?
        /// </summary>
        public bool Required { get; set; }

        /// <summary>
        /// Some delimiter
        /// </summary>
        public string ConstantValue { get; set; }

        public string PropertyCode { get; set; }
        public string Description { get; set; }

        public MeasureUnit Unit { get; set; }

        /// <summary>
        /// Values: 'string', 'number', 'enum', 'flags', 'range_min', 'range_max'
        /// Default: string
        /// 'number' - hex string, convert to number
        /// 'flags' - XOR bits with values from enums
        /// </summary>
        public string PropertyType { get; set; } = "string";

        /// <summary>
        /// PropertyCode for types range_min/range_max
        /// </summary>
        public string RangeSource { get; set; }

        public ICollection<EnumValue> Enums { get; set; }

        /// <summary>
        /// Whether to show the property value in summary table, near panel
        /// Default: false
        /// </summary>
        public bool ShowInSummary { get; set; }

        public ConditionsSet ShowInSummaryConditions { get; set; }
    }

    public class MeasureUnit
    {
        public string UnitCode { get; set; }

        /// <summary>
        /// DataValue * Multiplier => Real Value
        /// e.g. 0.1: 52 => 5.2 I
        /// </summary>
        public double Multiplier { get; set; }

        /// <summary>
        /// e.g. Base = 50
        /// 85 => 35
        /// 38 => -12
        /// Multiplier also may be applied
        /// </summary>
        public double Base { get; set; }

        /// <summary>
        /// Deprecated. Use Base.
        /// e.g. 100: '112' => -12
        /// </summary>
        public double NegativeBase { get; set; }

        /// <summary>
        /// Deprecated. Use Base.
        /// e.g. 200: '225' => 25
        /// </summary>
        public double PositiveBase { get; set; }


    }

    public struct EnumValue
    {
        public string Value;
        public string Description;
    }

    public class PresentationConfiguration
    {
        public ICollection<PresentationProperty> Properties { get; set; }

        public PresentationConfiguration()
        {
            Properties = new List<PresentationProperty>();
        }
    }

    public class PresentationProperty : IConditionsSet
    {
        /// <summary>
        /// Code of Property from messages (or ConstantText required)
        /// </summary>
        public string PropertyCode { get; set; }

        /// <summary>
        /// Constant text to output (when PropertyCode not defined)
        /// </summary>
        public string ConstantText { get; set; }

        /// <summary>
        /// Values are: 'text', 'led'
        /// </summary>
        public string Display { get; set; }

        /// <summary>
        /// Coordinates of top-left corner on PanelPhoto
        /// </summary>
        public Coordinates Offset { get; set; }

        /// <summary>
        /// Default will be ...
        /// </summary>
        public TextStyle TextStyle { get; set; }

        public LedStyle LedStyle { get; set; }

        /// <summary>
        /// Hex color (html color format)
        /// </summary>
        public string Color { get; set; }

        /// <summary>
        /// Default: false
        /// Negates the result of all conditions
        /// </summary>
        public bool ConditionNegative { get; set; }

        /// <summary>
        /// 'AND' or 'OR'
        /// Default: AND
        /// </summary>
        public string ConditionsRule { get; set; }

        public ICollection<PresentationCondition> Conditions { get; set; }
    }

    /// <summary>
    /// Must contain either rule for PropertyCode (PropertyCode, Operator, Value, ConditionNegative)
    /// or inner Conditions (Conditions, ConditionNegative, ConditionsRule)
    /// </summary>
    public class PresentationCondition : IConditionsSet
    {
        /// <summary>
        /// Either PropertyCode or array of inner Conditions
        /// </summary>
        public string PropertyCode { get; set; }

        /// <summary>
        /// e (equals), ne (not equals), lt, gt, lte, gte
        /// Default: e
        /// </summary>
        public string Operator { get; set; }

        public string Value { get; set; }

        /// <summary>
        /// Default: false
        /// Negates the result of this condition
        /// </summary>
        public bool ConditionNegative { get; set; }


        /// <summary>
        /// Rule: 'AND' or 'OR' for Conditions array
        /// Default: AND
        /// </summary>
        public string ConditionsRule { get; set; }

        /// <summary>
        /// Array of inner Conditions, if PropertyCode not defined
        /// </summary>
        public ICollection<PresentationCondition> Conditions { get; set; }
    }

    public class TextStyle
    {
        public string FontFamily;
        public double? FontSize;

        /// <summary>
        /// 'bold', 'italic', 'italicbold'
        /// </summary>
        public string FontStyle;

        public double NumberMultiplier { get; set; }
        public int NumberDigits { get; set; }
    }

    public class LedStyle
    {
        public double Width;
        public double Height;
        public double? Radius;
    }

    public struct Coordinates
    {
        public double X;
        public double Y;
    }

    public class ModeDefinitions
    {
        /// <summary>
        /// Режим сварки
        /// </summary>
        public ModeDefinition WeldingMode { get; set; }

        /// <summary>
        /// Режим Ошибка
        /// </summary>
        public ModeDefinition ErrorMode { get; set; }

        /// <summary>
        /// Режим оффлайн-данные
        /// </summary>
        public ModeDefinition OfflineDataMode { get; set; }

        /// <summary>
        /// Режим "Ограничения"
        /// </summary>
        public ModeDefinition LimitedMode { get; set; }

        /// <summary>
        /// Режим "Блокировка"
        /// </summary>
        public ModeDefinition BlockMode { get; set; }
    }

    public class ModeDefinition : IConditionsSet
    {
        public bool ConditionNegative { get; set; }
        public string ConditionsRule { get; set; }
        public ICollection<PresentationCondition> Conditions { get; set; }
    }

    public class AlertDefinition : IConditionsSet
    {
        public string Message { get; set; }

        public bool ConditionNegative { get; set; }
        public string ConditionsRule { get; set; }
        public ICollection<PresentationCondition> Conditions { get; set; }
    }

    public class ConditionsSet : IConditionsSet
    {
        public bool ConditionNegative { get; set; }
        public string ConditionsRule { get; set; }
        public ICollection<PresentationCondition> Conditions { get; set; }
    }

    public interface IConditionsSet
    {
        /// <summary>
        /// Default: false
        /// Negates the result of all conditions
        /// </summary>
        bool ConditionNegative { get; set; }

        /// <summary>
        /// 'AND' or 'OR' for Conditions array
        /// Default: AND
        /// </summary>
        string ConditionsRule { get; set; }

        ICollection<PresentationCondition> Conditions { get; set; }
    }
}
