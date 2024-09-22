using System;

namespace ZB_FEPMS.Models
{
    public class UserInfo
    {
        public Guid userId { get; set; }
        public string username { get; set; }
        public string fullName { get; set; }
        public string roleName { get; set; }

    }
}