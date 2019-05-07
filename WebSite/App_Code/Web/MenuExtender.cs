using System;
using System.Data;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Web;
using System.Web.UI;
using System.Web.UI.HtmlControls;
using System.Web.UI.WebControls;
using Planned.Data;
using Planned.Services;

namespace Planned.Web
{
	public enum MenuHoverStyle
    {
        
        Auto = 1,
        
        Click = 1,
        
        ClickAndStay = 1,
    }
    
    public enum MenuPresentationStyle
    {
        
        MultiLevel,
        
        TwoLevel,
        
        NavigationButton,
    }
    
    public enum MenuOrientation
    {
        
        Horizontal,
    }
    
    public enum MenuPopupPosition
    {
        
        Left,
        
        Right,
    }
    
    public enum MenuItemDescriptionStyle
    {
        
        None,
        
        Inline,
        
        ToolTip,
    }
    
    [TargetControlType(typeof(Panel))]
    [TargetControlType(typeof(HtmlContainerControl))]
    [DefaultProperty("TargetControlID")]
    public class MenuExtender : System.Web.UI.WebControls.HierarchicalDataBoundControl, IExtenderControl
    {
        
        private string _items;
        
        private ScriptManager _sm;
        
        private string _targetControlID;
        
        private bool _visible;
        
        private MenuHoverStyle _hoverStyle;
        
        private MenuPopupPosition _popupPosition;
        
        private MenuItemDescriptionStyle _itemDescriptionStyle;
        
        private bool _showSiteActions;
        
        [System.Diagnostics.DebuggerBrowsable(System.Diagnostics.DebuggerBrowsableState.Never)]
        private MenuPresentationStyle _presentationStyle;
        
        public Regex MenuItemPropRegex = new Regex("\\s*(?\'Name\'\\w+)\\s*(=|:)\\s*(?\'Value\'.+?)\\s*(\\r?\\n|$)");
        
        public static Regex MenuItemRegex = new Regex("(?\'Text\'(?\'Depth\'(#|\\+|\\^)+)\\s*(?\'Title\'.+?)\\r?\\n(?\'Url\'.*?)(\\r?\\n|$)(?\'PropList\'" +
                "(\\s*\\w+\\s*(:|=)\\s*.+?(\\r?\\n|$))*))");
        
        public MenuExtender() : 
                base()
        {
            this.Visible = true;
            ItemDescriptionStyle = MenuItemDescriptionStyle.ToolTip;
            HoverStyle = MenuHoverStyle.Auto;
        }
        
        [IDReferenceProperty]
        [Category("Behavior")]
        [DefaultValue("")]
        public string TargetControlID
        {
            get
            {
                return _targetControlID;
            }
            set
            {
                _targetControlID = value;
            }
        }
        
        [EditorBrowsable(EditorBrowsableState.Never)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        [Browsable(false)]
        public override bool Visible
        {
            get
            {
                return _visible;
            }
            set
            {
                _visible = value;
            }
        }
        
        public MenuHoverStyle HoverStyle
        {
            get
            {
                return _hoverStyle;
            }
            set
            {
                _hoverStyle = value;
            }
        }
        
        public MenuPopupPosition PopupPosition
        {
            get
            {
                return _popupPosition;
            }
            set
            {
                _popupPosition = value;
            }
        }
        
        public MenuItemDescriptionStyle ItemDescriptionStyle
        {
            get
            {
                return _itemDescriptionStyle;
            }
            set
            {
                _itemDescriptionStyle = value;
            }
        }
        
        [System.ComponentModel.Description("The \"Site Actions\" menu is automatically displayed.")]
        [System.ComponentModel.DefaultValue(false)]
        public bool ShowSiteActions
        {
            get
            {
                return _showSiteActions;
            }
            set
            {
                _showSiteActions = value;
            }
        }
        
        [System.ComponentModel.Description("Specifies the menu presentation style.")]
        [System.ComponentModel.DefaultValue(MenuPresentationStyle.MultiLevel)]
        public MenuPresentationStyle PresentationStyle
        {
            get
            {
                return this._presentationStyle;
            }
            set
            {
                this._presentationStyle = value;
            }
        }
        
        protected override void PerformDataBinding()
        {
            base.PerformDataBinding();
            if (!(IsBoundUsingDataSourceID) && (DataSource != null))
            	return;
            HierarchicalDataSourceView view = GetData(String.Empty);
            IHierarchicalEnumerable enumerable = view.Select();
            if (ApplicationServices.IsSiteContentEnabled && !(ApplicationServices.IsSafeMode))
            {
                SiteContentFileList sitemaps = ApplicationServices.Current.ReadSiteContent("sys/sitemaps%", "%");
                if (sitemaps.Count > 0)
                {
                    bool hasMain = false;
                    foreach (SiteContentFile f in sitemaps)
                    	if (f.PhysicalName == "main")
                        {
                            hasMain = true;
                            sitemaps.Remove(f);
                            sitemaps.Insert(0, f);
                            break;
                        }
                    if (!(hasMain) && (enumerable != null))
                    {
                        StringBuilder msb = new StringBuilder();
                        BuildMainMenu(enumerable, msb, 1);
                        SiteContentFile main = new SiteContentFile();
                        main.Text = Localizer.Replace("Pages", Path.GetFileName(Page.Request.PhysicalPath), msb.ToString());
                        sitemaps.Insert(0, main);
                    }
                    string text = null;
                    if (sitemaps.Count > 1)
                    {
                        SiteMapBuilder sm = new SiteMapBuilder();
                        foreach (SiteContentFile cf in sitemaps)
                        {
                            string sitemapText = cf.Text;
                            if (!(String.IsNullOrEmpty(sitemapText)))
                            {
                                MatchCollection coll = MenuItemRegex.Matches(sitemapText);
                                foreach (Match m in coll)
                                	sm.Insert(m.Groups["Title"].Value, m.Groups["Depth"].Value.Length, m.Groups["Text"].Value);
                            }
                        }
                        text = sm.ToString();
                    }
                    else
                    	text = sitemaps[0].Text;
                    StringBuilder sb = new StringBuilder();
                    if (!(String.IsNullOrEmpty(text)))
                    {
                        bool first = true;
                        Match m = MenuItemRegex.Match(text);
                        while (m.Success)
                        {
                            BuildNode(ref m, sb, first);
                            if (first)
                            	first = false;
                        }
                        _items = Regex.Replace(sb.ToString(), "(\\{\\}\\,?)+", String.Empty).Replace("},]", "}]");
                        return;
                    }
                }
            }
            if (enumerable != null)
            {
                StringBuilder sb = new StringBuilder();
                RecursiveDataBindInternal(enumerable, sb);
                _items = sb.ToString();
            }
        }
        
        private void BuildMainMenu(IHierarchicalEnumerable enumerable, StringBuilder sb, int depth)
        {
            foreach (object item in enumerable)
            {
                IHierarchyData data = enumerable.GetHierarchyData(item);
                if (data != null)
                {
                    PropertyDescriptorCollection props = TypeDescriptor.GetProperties(data);
                    if (props.Count > 0)
                    {
                        string title = ((string)(props["Title"].GetValue(data)));
                        string description = ((string)(props["Description"].GetValue(data)));
                        string url = ((string)(props["Url"].GetValue(data)));
                        string cssClass = null;
                        string roles = null;
                        ArrayList roleList = ((ArrayList)(props["Roles"].GetValue(data)));
                        if (roleList.Count > 0)
                        	roles = String.Join(",", ((string[])(roleList.ToArray(typeof(string)))));
                        if (item is SiteMapNode)
                        {
                            cssClass = ((SiteMapNode)(item))["cssClass"];
                            if ("true" == ((SiteMapNode)(item))["public"])
                            	roles = "?";
                        }
                        sb.AppendFormat("{0} {1}", new String('+', depth), title);
                        sb.AppendLine();
                        if (!(String.IsNullOrEmpty(url)))
                        	sb.AppendLine(url);
                        else
                        	sb.AppendLine("about:blank");
                        if (!(String.IsNullOrEmpty(description)))
                        {
                            sb.AppendFormat("description: {0}", description);
                            sb.AppendLine();
                        }
                        if (!(String.IsNullOrEmpty(roles)))
                        {
                            sb.AppendFormat("roles: {0}", roles);
                            sb.AppendLine();
                        }
                        if (!(String.IsNullOrEmpty(cssClass)))
                        {
                            sb.AppendFormat("cssclass: {0}", cssClass);
                            sb.AppendLine();
                        }
                        sb.AppendLine();
                        if (data.HasChildren)
                        {
                            IHierarchicalEnumerable childrenEnumerable = data.GetChildren();
                            if (childrenEnumerable != null)
                            	BuildMainMenu(childrenEnumerable, sb, (depth + 1));
                        }
                    }
                }
            }
        }
        
        private void BuildNode(ref Match node, StringBuilder sb, bool first)
        {
            if (!(first))
            	sb.Append(",");
            SortedDictionary<string, string> propList = new SortedDictionary<string, string>();
            Match prop = MenuItemPropRegex.Match(node.Groups["PropList"].Value);
            while (prop.Success)
            {
                propList[prop.Groups["Name"].Value.ToLower().Replace("-", String.Empty)] = prop.Groups["Value"].Value;
                prop = prop.NextMatch();
            }
            string roles = null;
            propList.TryGetValue("roles", out roles);
            string users = null;
            propList.TryGetValue("users", out users);
            string roleExceptions = null;
            propList.TryGetValue("roleexceptions", out roleExceptions);
            string userExceptions = null;
            propList.TryGetValue("userexceptions", out userExceptions);
            string cssClass = null;
            propList.TryGetValue("cssclass", out cssClass);
            string url = node.Groups["Url"].Value.Trim();
            string target = null;
            if (url.StartsWith("_blank:"))
            {
                target = "_blank:";
                url = url.Substring(7);
            }
            url = ResolveUrl(url);
            if (!(String.IsNullOrEmpty(target)))
            	url = (target + url);
            bool resourceAuthorized = true;
            if (!(String.IsNullOrEmpty(roles)))
            {
                if (!(ApplicationServices.UserIsAuthorizedToAccessResource(url, roles)))
                	resourceAuthorized = false;
            }
            if (resourceAuthorized && !(String.IsNullOrEmpty(users)))
            {
                if (!((users == "?")) && (Array.IndexOf(users.ToLower().Split(new char[] {
                                            ','}, StringSplitOptions.RemoveEmptyEntries), Page.User.Identity.Name.ToLower()) == -1))
                	resourceAuthorized = false;
            }
            if (!(resourceAuthorized) && !(String.IsNullOrEmpty(roleExceptions)))
            {
                if (DataControllerBase.UserIsInRole(roleExceptions))
                	resourceAuthorized = true;
            }
            if (!(resourceAuthorized) && !(String.IsNullOrEmpty(userExceptions)))
            {
                if (!((Array.IndexOf(userExceptions.ToLower().Split(new char[] {
                                            ','}, StringSplitOptions.RemoveEmptyEntries), Page.User.Identity.Name.ToLower()) == -1)))
                	resourceAuthorized = true;
            }
            sb.Append("{");
            if (resourceAuthorized)
            {
                string title = node.Groups["Title"].Value.Trim();
                string depth = node.Groups["Depth"].Value;
                sb.AppendFormat("title:\"{0}\"", BusinessRules.JavaScriptString(title));
                if (!((url == "about:blank")))
                	sb.AppendFormat(",url:\"{0}\"", BusinessRules.JavaScriptString(url));
                if (Page.Request.RawUrl == url)
                	sb.Append(",selected:true");
                string description = null;
                propList.TryGetValue("description", out description);
                if (!(String.IsNullOrEmpty(description)))
                	sb.AppendFormat(",description:\"{0}\"", BusinessRules.JavaScriptString(description));
                if (!(String.IsNullOrEmpty(cssClass)))
                	sb.AppendFormat(",cssClass:\"{0}\"", BusinessRules.JavaScriptString(cssClass));
                node = node.NextMatch();
                if (node.Success)
                {
                    string firstChildDepth = node.Groups["Depth"].Value;
                    if (firstChildDepth.Length > depth.Length)
                    {
                        sb.Append(",children:[");
                        first = true;
                        while (node.Success)
                        {
                            BuildNode(ref node, sb, first);
                            if (first)
                            	first = false;
                            if (node.Success)
                            {
                                string nextDepth = node.Groups["Depth"].Value;
                                if (firstChildDepth.Length > nextDepth.Length)
                                	break;
                            }
                        }
                        sb.Append("]");
                    }
                }
            }
            else
            	node = node.NextMatch();
            sb.Append("}");
        }
        
        private void RecursiveDataBindInternal(IHierarchicalEnumerable enumerable, StringBuilder sb)
        {
            bool first = true;
            if (this.Site != null)
            	return;
            foreach (object item in enumerable)
            {
                IHierarchyData data = enumerable.GetHierarchyData(item);
                if (null != data)
                {
                    PropertyDescriptorCollection props = TypeDescriptor.GetProperties(data);
                    if (props.Count > 0)
                    {
                        string title = ((string)(props["Title"].GetValue(data)));
                        string description = ((string)(props["Description"].GetValue(data)));
                        string url = ((string)(props["Url"].GetValue(data)));
                        string cssClass = null;
                        bool isPublic = false;
                        if (item is SiteMapNode)
                        {
                            cssClass = ((SiteMapNode)(item))["cssClass"];
                            isPublic = ("true" == ((string)(((SiteMapNode)(item))["public"])));
                        }
                        string roles = String.Empty;
                        ArrayList roleList = ((ArrayList)(props["Roles"].GetValue(data)));
                        if (roleList.Count > 0)
                        	roles = String.Join(",", ((string[])(roleList.ToArray(typeof(string)))));
                        bool resourceAuthorized = true;
                        if (resourceAuthorized)
                        {
                            if (first)
                            	first = false;
                            else
                            	sb.Append(",");
                            sb.AppendFormat("{{title:\"{0}\",url:\"{1}\"", BusinessRules.JavaScriptString(title), BusinessRules.JavaScriptString(url));
                            if (!(String.IsNullOrEmpty(description)))
                            	sb.AppendFormat(",description:\"{0}\"", BusinessRules.JavaScriptString(description));
                            if (url == Page.Request.RawUrl)
                            	sb.Append(",selected:true");
                            if (!(String.IsNullOrEmpty(cssClass)))
                            	sb.AppendFormat(",cssClass:\"{0}\"", cssClass);
                            if (data.HasChildren)
                            {
                                IHierarchicalEnumerable childrenEnumerable = data.GetChildren();
                                if (null != childrenEnumerable)
                                {
                                    sb.Append(",\"children\":[");
                                    RecursiveDataBindInternal(childrenEnumerable, sb);
                                    sb.Append("]");
                                }
                            }
                            sb.Append("}");
                        }
                    }
                }
            }
        }
        
        protected override void OnInit(EventArgs e)
        {
            base.OnInit(e);
            _sm = ScriptManager.GetCurrent(Page);
        }
        
        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            AquariumExtenderBase.RegisterFrameworkSettings(Page);
        }
        
        protected override void OnPreRender(EventArgs e)
        {
            base.OnPreRender(e);
            if (null == _sm)
            	return;
            string script = String.Format("Web.Menu.Nodes.{0}=[{1}];", this.ClientID, _items);
            Control target = Page.Form.FindControl(TargetControlID);
            if ((null != target) && target.Visible)
            	ScriptManager.RegisterStartupScript(this, typeof(MenuExtender), "Nodes", script, true);
            _sm.RegisterExtenderControl<MenuExtender>(this, target);
        }
        
        protected override void Render(HtmlTextWriter writer)
        {
            bool isTouchUI = ApplicationServices.IsTouchClient;
            if ((null == _sm) || (_sm.IsInAsyncPostBack || isTouchUI))
            	return;
            _sm.RegisterScriptDescriptors(this);
        }
        
        IEnumerable<ScriptDescriptor> IExtenderControl.GetScriptDescriptors(Control targetControl)
        {
            ScriptBehaviorDescriptor descriptor = new ScriptBehaviorDescriptor("Web.Menu", targetControl.ClientID);
            descriptor.AddProperty("id", this.ClientID);
            if (HoverStyle != MenuHoverStyle.Auto)
            	descriptor.AddProperty("hoverStyle", Convert.ToInt32(HoverStyle));
            if (PopupPosition != MenuPopupPosition.Left)
            	descriptor.AddProperty("popupPosition", Convert.ToInt32(PopupPosition));
            if (ItemDescriptionStyle != MenuItemDescriptionStyle.ToolTip)
            	descriptor.AddProperty("itemDescriptionStyle", Convert.ToInt32(ItemDescriptionStyle));
            if (ShowSiteActions)
            	descriptor.AddProperty("showSiteActions", "true");
            if (PresentationStyle != MenuPresentationStyle.MultiLevel)
            	descriptor.AddProperty("presentationStyle", Convert.ToInt32(PresentationStyle));
            return new ScriptBehaviorDescriptor[] {
                    descriptor};
        }
        
        IEnumerable<ScriptReference> IExtenderControl.GetScriptReferences()
        {
            return AquariumExtenderBase.StandardScripts();
        }
    }
    
    public class SiteMapBuilder
    {
        
        private SiteMapBuilderNode _root = new SiteMapBuilderNode(String.Empty, 0, String.Empty);
        
        private SiteMapBuilderNode _last;
        
        public void Insert(string title, int depth, string text)
        {
            if (_last == null)
            	_last = _root;
            SiteMapBuilderNode entry = new SiteMapBuilderNode(title, depth, text);
            _last = _last.AddNode(entry);
        }
        
        public override string ToString()
        {
            return _root.ToString();
        }
    }
    
    public class SiteMapBuilderNode
    {
        
        [System.Diagnostics.DebuggerBrowsable(System.Diagnostics.DebuggerBrowsableState.Never)]
        private string _title;
        
        [System.Diagnostics.DebuggerBrowsable(System.Diagnostics.DebuggerBrowsableState.Never)]
        private int _depth;
        
        [System.Diagnostics.DebuggerBrowsable(System.Diagnostics.DebuggerBrowsableState.Never)]
        private string _text;
        
        [System.Diagnostics.DebuggerBrowsable(System.Diagnostics.DebuggerBrowsableState.Never)]
        private SiteMapBuilderNode _parent;
        
        [System.Diagnostics.DebuggerBrowsable(System.Diagnostics.DebuggerBrowsableState.Never)]
        private List<SiteMapBuilderNode> _children;
        
        public SiteMapBuilderNode(string title, int depth, string text)
        {
            this.Title = title;
            this.Depth = depth;
            this.Text = text;
            Children = new List<SiteMapBuilderNode>();
        }
        
        public string Title
        {
            get
            {
                return this._title;
            }
            set
            {
                this._title = value;
            }
        }
        
        public int Depth
        {
            get
            {
                return this._depth;
            }
            set
            {
                this._depth = value;
            }
        }
        
        public string Text
        {
            get
            {
                return this._text;
            }
            set
            {
                this._text = value;
            }
        }
        
        public SiteMapBuilderNode Parent
        {
            get
            {
                return this._parent;
            }
            set
            {
                this._parent = value;
            }
        }
        
        public List<SiteMapBuilderNode> Children
        {
            get
            {
                return this._children;
            }
            set
            {
                this._children = value;
            }
        }
        
        public SiteMapBuilderNode AddNode(SiteMapBuilderNode entry)
        {
            // go up
            if (entry.Depth <= Depth)
            	return Parent.AddNode(entry);
            else
            {
                // current child
                foreach (SiteMapBuilderNode child in Children)
                	if (child.Title == entry.Title)
                    {
                        if (!(String.IsNullOrWhiteSpace(entry.Text.Replace(entry.Title, String.Empty).Replace("+", String.Empty))))
                        	child.Text = entry.Text;
                        return child;
                    }
                Children.Add(entry);
                entry.Parent = this;
                return entry;
            }
        }
        
        public override string ToString()
        {
            StringBuilder sb = new StringBuilder();
            if (!(String.IsNullOrEmpty(Text)))
            	sb.AppendLine(Text);
            foreach (SiteMapBuilderNode entry in Children)
            	sb.AppendLine(entry.ToString());
            return sb.ToString();
        }
    }
}
