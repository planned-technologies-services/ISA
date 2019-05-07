using System;
using System.Data;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Web;
using System.Web.Security;
using Planned.Data;

namespace Planned.Rules
{
	public partial class IsaDetailPbsBusinessRules : Planned.Data.BusinessRules
    {
        
        [RowBuilder("IsaDetailPbs", RowKind.New)]
        public void BuildNewIsaDetailPbs()
        {
            UpdateFieldValue("PBSCopanyCode", "PBS");
        }
    }
}
