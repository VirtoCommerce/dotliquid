using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DotLiquid
{
    public interface ILoadSlice
    {
        void LoadSlice(int from, int? to);
    }
}
