//------------------------------------------------------------------------------
// <auto-generated>
//     This code was generated from a template.
//
//     Manual changes to this file may cause unexpected behavior in your application.
//     Manual changes to this file will be overwritten if the code is regenerated.
// </auto-generated>
//------------------------------------------------------------------------------

namespace ZB_FEPMS.Models
{
    using System;
    using System.Collections.Generic;
    
    public partial class tblPOPermitExpiry
    {
        public tblPOPermitExpiry()
        {
            this.Id = Guid.NewGuid();
        }
        public System.Guid Id { get; set; }
        public System.Guid PermitId { get; set; }
        public System.DateTime ExpiryDate { get; set; }
        public Nullable<bool> IsExtension { get; set; }
        public Nullable<bool> ChargeCollected { get; set; }
    
        public virtual tblPermit tblPermit { get; set; }
    }
}
