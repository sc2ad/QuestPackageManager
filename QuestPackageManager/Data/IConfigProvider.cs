using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace QuestPackageManager.Data
{
    public interface IConfigProvider
    {
        string ToString(Config? config);

        Config? From(string data);

        SharedConfig? GetSharedConfig(bool createOnFail = false);

        Config? GetConfig(bool createOnFail = false);

        void Commit();
    }
}