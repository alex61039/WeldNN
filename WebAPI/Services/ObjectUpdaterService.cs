using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace WebAPI.Services
{
    public class ObjectUpdaterService
    {
        static public readonly string ALL = "*";

        static public void ObjectUpdated(DataLayer.Welding.WeldingContext context, string ObjectType)
        {
            context.ObjectUpdates.Add(new DataLayer.Welding.ObjectUpdate {
                DateCreated = DateTime.Now,
                ObjectType = ObjectType
            });

            context.SaveChanges();
        }
    }
}
