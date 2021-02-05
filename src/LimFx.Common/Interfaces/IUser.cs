using System;
using System.Collections.Generic;

namespace LimFx.Business.Models
{
    public interface IUser:IEntity
    {
        string Email { get; set; }
        bool IsEmailConfirmed { get; set; }
        List<string> Roles { get; set; }
        int Follows { get; set; }
        int Followers { get; set; }
        string PassWordHash { get; set; }
        string Name { get; set; }
        string AvatarUrl { get; set; }
        float Exp { get; set; }

        public string SecurityStamp { get; set; }
    }
    public interface IUserBrief
    {
        Guid Id { get; set; }
        string Email { get; set; }
    }
}
