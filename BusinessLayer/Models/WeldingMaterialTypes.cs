using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BusinessLayer.Models
{
    public enum WeldingMaterialTypeEnum
    {
        Wire = 1,               // проволока
        Gas = 2,
        Flux = 3,               // флюс
        Electrode = 4,
        WeldedMaterial = 5      // свариваемый материал!
    }
}
