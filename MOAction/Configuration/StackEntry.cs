﻿using MOAction.Target;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MOAction.Configuration
{
    public class StackEntry
    {
        public Lumina.Excel.Sheets.Action Action;
        public TargetType Target { get; set; }

        public StackEntry(Lumina.Excel.Sheets.Action action, TargetType targ)
        {
            Action = action;
            Target = targ;
        }
    }
}
