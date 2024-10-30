using BusinessLayer.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BusinessLayer.Utils
{
    public class ScancodesHelper
    {
        static public Dictionary<string, string> EntitiesMap = new Dictionary<string, string> {
            {"01", "WeldingAssemblyControl"},
            {"02", "WeldingAssemblyControlResult"},
            {"03", "WeldingAssemblyInstruction"},
            {"04", "DetailAssembly"},
            {"05", "DetailAssemblyType"},
            {"06", "DetailPart"},
            {"07", "DetailPartType"},
            {"08", "UserAccount"},
            {"09", "NetworkDevice"},
            {"10", "WeldingMachine"},
            {"11", "WeldingMaterial"},
            {"12", "WeldingLimitProgram"},
            {"13", "Maintenance"}
        };

        static public string GenerateCode(bool UseQR, ScancodeEntity entity)
        {
            var code = "";

            // QR or Barcode?
            if (UseQR)
                code = String.Format("WeldTelecom|v1|{0}|{1}", entity.Entity, entity.ID);
            else
            {
                if (EntitiesMap.ContainsValue(entity.Entity))
                {
                    var entity_key = EntitiesMap.FirstOrDefault(d => d.Value == entity.Entity).Key;

                    // 2 ver key id
                    code = String.Format("2{0}{1}{2}",
                        "1",
                        entity_key.PadLeft(2, '0'),
                        entity.ID.PadLeft(8, '0')
                        );

                    // control sum
                    var arr = Enumerable.Range(0, code.Length)
                        .Select(i => Convert.ToInt32(code.Substring(i, 1)))
                        .ToArray();

                    var sum = (arr[11] * 3) + arr[10] + (arr[9] * 3) + arr[8] + (arr[7] * 3) + arr[6] + (arr[5] * 3) + arr[4] + (arr[3] * 3) + arr[2] + (arr[1] * 3) + arr[0];
                    sum = (10 - (sum % 10)) % 10;

                    code = code + sum.ToString();
                }
            }

            return code;
        }

        static public bool TryParse(string scancode, out ScancodeEntity entity)
        {
            entity = new ScancodeEntity();

            if (String.IsNullOrEmpty(scancode))
                return false;

            // QR code?
            if (scancode.IndexOf("WeldTelecom|v1") == 0)
            {
                var arr = scancode.Split('|');
                if (arr.Length == 4)
                {
                    entity.Entity = arr[2];
                    entity.ID = arr[3];

                    return true;
                }
            }

            // Barcode?
            if (scancode.Length == 13)
            {
                // 2 1   01     0000000000
                // 2 ver Entity ID
                var ver = scancode.Substring(1, 1);             //'1'
                var entity_key = scancode.Substring(2, 2);      // '01'
                var entity_id = scancode.Substring(4, 8).TrimStart('0');

                // entity key exists?
                if (EntitiesMap.ContainsKey(entity_key))
                {
                    entity.Entity = EntitiesMap[entity_key];
                    entity.ID = entity_id;

                    return true;
                }
            }

            return false;
        }

    }
}
