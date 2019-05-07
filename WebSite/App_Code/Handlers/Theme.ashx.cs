using System;
using System.Web;
using Planned.Data;
using Planned.Services;

namespace Planned.Handlers
{
	public partial class Theme : GenericHandlerBase, IHttpHandler, System.Web.SessionState.IRequiresSessionState
    {
        
        bool IHttpHandler.IsReusable
        {
            get
            {
                return true;
            }
        }
        
        void IHttpHandler.ProcessRequest(HttpContext context)
        {
            string theme = context.Request.QueryString["theme"];
            string accent = context.Request.QueryString["accent"];
            if (String.IsNullOrEmpty(theme) || String.IsNullOrEmpty(accent))
            	throw new HttpException(400, "Bad Request");
            ApplicationServices services = new ApplicationServices();
            string css = ApplicationServices.Current.CompileThemeStylesheet(String.Format("touch-theme.{0}.{1}.css", theme, accent));
            context.Response.ContentType = "text/css";
            HttpCachePolicy cache = context.Response.Cache;
            cache.SetCacheability(HttpCacheability.Public);
            cache.SetOmitVaryStar(true);
            cache.SetExpires(System.DateTime.Now.AddDays(365));
            cache.SetValidUntilExpires(true);
            cache.SetLastModifiedFromFileDependencies();
            ApplicationServices.CompressOutput(context, css);
        }
    }
}
