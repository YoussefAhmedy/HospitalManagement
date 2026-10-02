using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Hospital.DAL.Entities.OwnedTypes
{
     public enum AppointStatus
    {
        Approved = 1,
        NotApproved = 2,
        Pending = 3,
        Cancelled = 4
    }
}
