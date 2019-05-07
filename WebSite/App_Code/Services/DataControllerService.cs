using System;
using System.Collections.Generic;
using System.Text;
using System.Web;
using System.Web.Services;
using System.Web.Script.Services;
using Planned.Data;

namespace Planned.Services
{
	public class DataControllerService
    {
        
        public DataControllerService()
        {
        }
        
        protected List<string[]> Permalinks
        {
            get
            {
                List<string[]> links = ((List<string[]>)(HttpContext.Current.Session["Permalinks"]));
                if (links == null)
                {
                    links = new List<string[]>();
                    HttpContext.Current.Session["Permalinks"] = links;
                }
                return links;
            }
        }
        
        public ViewPage GetPage(string controller, string view, PageRequest request)
        {
            return ControllerFactory.CreateDataController().GetPage(controller, view, request);
        }
        
        public ActionResult[] ExecuteList(ActionArgs[] requests)
        {
            return ((DataControllerBase)(ControllerFactory.CreateDataController())).ExecuteList(requests);
        }
        
        public ViewPage[] GetPageList(PageRequest[] requests)
        {
            return ((DataControllerBase)(ControllerFactory.CreateDataController())).GetPageList(requests);
        }
        
        public object[] GetListOfValues(string controller, string view, DistinctValueRequest request)
        {
            return ControllerFactory.CreateDataController().GetListOfValues(controller, view, request);
        }
        
        public ActionResult Execute(string controller, string view, ActionArgs args)
        {
            return ControllerFactory.CreateDataController().Execute(controller, view, args);
        }
        
        public string[] GetCompletionList(string prefixText, int count, string contextKey)
        {
            return ControllerFactory.CreateAutoCompleteManager().GetCompletionList(prefixText, count, contextKey);
        }
        
        protected string[] FindPermalink(string link)
        {
            foreach (string[] entry in Permalinks)
            	if (entry[0] == link)
                	return entry;
            return null;
        }
        
        public void SavePermalink(string link, string html)
        {
            string[] permalink = FindPermalink(link);
            if (Permalinks.Contains(permalink))
            	Permalinks.Remove(permalink);
            if (!(String.IsNullOrEmpty(html)))
            	Permalinks.Insert(0, new string[] {
                            link,
                            html});
            else
            	if (Permalinks.Count > 0)
                	Permalinks.RemoveAt(0);
            while (Permalinks.Count > 10)
            	Permalinks.RemoveAt((Permalinks.Count - 1));
        }
        
        public string EncodePermalink(string link, bool rooted)
        {
            HttpRequest request = HttpContext.Current.Request;
            StringEncryptor enc = new StringEncryptor();
            if (rooted)
            {
                string appPath = request.ApplicationPath;
                if (appPath.Equals("/"))
                	appPath = String.Empty;
                return String.Format("{0}://{1}{2}/default.aspx?_link={3}", request.Url.Scheme, request.Url.Authority, appPath, HttpUtility.UrlEncode(enc.Encrypt(link)));
            }
            else
            {
                string[] linkSegments = link.Split('?');
                string arguments = String.Empty;
                if (linkSegments.Length > 1)
                	arguments = linkSegments[1];
                return String.Format("{0}?_link={1}", linkSegments[0], HttpUtility.UrlEncode(enc.Encrypt(arguments)));
            }
        }
        
        public string[][] ListAllPermalinks()
        {
            return Permalinks.ToArray();
        }
        
        public string GetSurvey(string survey)
        {
            return ControllerFactory.GetSurvey(survey);
        }
        
        public object Login(string username, string password, bool createPersistentCookie)
        {
            return ApplicationServices.Login(username, password, createPersistentCookie);
        }
        
        public void Logout()
        {
            ApplicationServices.Logout();
        }
        
        public string[] Roles()
        {
            return ApplicationServices.Roles();
        }
        
        public object Themes()
        {
            return ApplicationServices.Themes();
        }
    }
}
