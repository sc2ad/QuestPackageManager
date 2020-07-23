using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace QuestPackageManager.Data
{
    public interface IConfigProvider
    {
        Config? GetConfig(bool createOnFail = false);

        void Commit();
    }
}