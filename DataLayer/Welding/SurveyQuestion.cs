//------------------------------------------------------------------------------
// <auto-generated>
//     This code was generated from a template.
//
//     Manual changes to this file may cause unexpected behavior in your application.
//     Manual changes to this file will be overwritten if the code is regenerated.
// </auto-generated>
//------------------------------------------------------------------------------

namespace DataLayer.Welding
{
    using System;
    using System.Collections.Generic;
    
    public partial class SurveyQuestion
    {
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Usage", "CA2214:DoNotCallOverridableMethodsInConstructors")]
        public SurveyQuestion()
        {
            this.SurveyPassQuestionAnswers = new HashSet<SurveyPassQuestionAnswer>();
            this.SurveyQuestionOptions = new HashSet<SurveyQuestionOption>();
        }
    
        public int ID { get; set; }
    
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Usage", "CA2227:CollectionPropertiesShouldBeReadOnly")]
        public virtual ICollection<SurveyPassQuestionAnswer> SurveyPassQuestionAnswers { get; set; }
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Usage", "CA2227:CollectionPropertiesShouldBeReadOnly")]
        public virtual ICollection<SurveyQuestionOption> SurveyQuestionOptions { get; set; }
    }
}
