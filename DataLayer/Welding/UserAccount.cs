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
    
    public partial class UserAccount
    {
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Usage", "CA2214:DoNotCallOverridableMethodsInConstructors")]
        public UserAccount()
        {
            this.InboxMessages = new HashSet<InboxMessage>();
            this.InboxMessages1 = new HashSet<InboxMessage>();
            this.Notifications = new HashSet<Notification>();
            this.Organizations = new HashSet<Organization>();
            this.OrganizationUnits = new HashSet<OrganizationUnit>();
            this.SurveyPasses = new HashSet<SurveyPass>();
            this.UserAccountSessions = new HashSet<UserAccountSession>();
            this.UserTokens = new HashSet<UserToken>();
            this.WeldingLimitPrograms = new HashSet<WeldingLimitProgram>();
            this.UserActs = new HashSet<UserAct>();
        }
    
        public int ID { get; set; }
        public Nullable<int> OrganizationUnitID { get; set; }
        public int UserRoleID { get; set; }
        public int Status { get; set; }
        public System.DateTime DateCreated { get; set; }
        public Nullable<System.DateTime> DateLastLogon { get; set; }
        public string UserName { get; set; }
        public byte[] PasswordHash { get; set; }
        public string PasswordSalt { get; set; }
        public string Name { get; set; }
        public Nullable<int> FailedLoginsCount { get; set; }
        public string Email { get; set; }
        public string Position { get; set; }
        public string Category { get; set; }
        public string PersonnelNumber { get; set; }
        public string RFID { get; set; }
        public Nullable<System.DateTime> RecruitmentDate { get; set; }
        public Nullable<System.DateTime> BirthDate { get; set; }
        public Nullable<System.Guid> Photo { get; set; }
        public string Education { get; set; }
        public string Phone { get; set; }
        public string Address { get; set; }
        public string Description { get; set; }
        public string RFID_Hex { get; set; }
        public Nullable<bool> AllowEmailNotifications { get; set; }
    
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Usage", "CA2227:CollectionPropertiesShouldBeReadOnly")]
        public virtual ICollection<InboxMessage> InboxMessages { get; set; }
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Usage", "CA2227:CollectionPropertiesShouldBeReadOnly")]
        public virtual ICollection<InboxMessage> InboxMessages1 { get; set; }
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Usage", "CA2227:CollectionPropertiesShouldBeReadOnly")]
        public virtual ICollection<Notification> Notifications { get; set; }
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Usage", "CA2227:CollectionPropertiesShouldBeReadOnly")]
        public virtual ICollection<Organization> Organizations { get; set; }
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Usage", "CA2227:CollectionPropertiesShouldBeReadOnly")]
        public virtual ICollection<OrganizationUnit> OrganizationUnits { get; set; }
        public virtual OrganizationUnit OrganizationUnit { get; set; }
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Usage", "CA2227:CollectionPropertiesShouldBeReadOnly")]
        public virtual ICollection<SurveyPass> SurveyPasses { get; set; }
        public virtual UserRole UserRole { get; set; }
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Usage", "CA2227:CollectionPropertiesShouldBeReadOnly")]
        public virtual ICollection<UserAccountSession> UserAccountSessions { get; set; }
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Usage", "CA2227:CollectionPropertiesShouldBeReadOnly")]
        public virtual ICollection<UserToken> UserTokens { get; set; }
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Usage", "CA2227:CollectionPropertiesShouldBeReadOnly")]
        public virtual ICollection<WeldingLimitProgram> WeldingLimitPrograms { get; set; }
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Usage", "CA2227:CollectionPropertiesShouldBeReadOnly")]
        public virtual ICollection<UserAct> UserActs { get; set; }
    }
}