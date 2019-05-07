using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Data.Common;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.XPath;
using System.Web;
using System.Web.Caching;
using System.Web.Configuration;
using System.Web.Security;

namespace Planned.Data
{
	[Serializable]
    public class FieldValue
    {
        
        [System.Diagnostics.DebuggerBrowsable(System.Diagnostics.DebuggerBrowsableState.Never)]
        private string _name;
        
        [System.Diagnostics.DebuggerBrowsable(System.Diagnostics.DebuggerBrowsableState.Never)]
        private object _oldValue;
        
        [System.Diagnostics.DebuggerBrowsable(System.Diagnostics.DebuggerBrowsableState.Never)]
        private object _newValue;
        
        [System.Diagnostics.DebuggerBrowsable(System.Diagnostics.DebuggerBrowsableState.Never)]
        private bool _noCheck;
        
        [System.Diagnostics.DebuggerBrowsable(System.Diagnostics.DebuggerBrowsableState.Never)]
        private bool _modified;
        
        [System.Diagnostics.DebuggerBrowsable(System.Diagnostics.DebuggerBrowsableState.Never)]
        private bool _readOnly;
        
        [System.Diagnostics.DebuggerBrowsable(System.Diagnostics.DebuggerBrowsableState.Never)]
        private string _error;
        
        private bool _enableConversion = true;
        
        public FieldValue()
        {
        }
        
        public FieldValue(string fieldName)
        {
            this._name = fieldName;
        }
        
        public FieldValue(string fieldName, object newValue) : 
                this(fieldName, null, newValue)
        {
        }
        
        public FieldValue(string fieldName, object oldValue, object newValue)
        {
            this._name = fieldName;
            this._oldValue = oldValue;
            this._newValue = newValue;
            CheckModified();
        }
        
        public FieldValue(string fieldName, object oldValue, object newValue, bool readOnly) : 
                this(fieldName, oldValue, newValue)
        {
            this._readOnly = readOnly;
        }
        
        public string Name
        {
            get
            {
                return _name;
            }
            set
            {
                _name = value;
            }
        }
        
        public object OldValue
        {
            get
            {
                return _oldValue;
            }
            set
            {
                if (_enableConversion && (value is string))
                	_oldValue = Controller.StringToValue(((string)(value)));
                else
                	_oldValue = value;
            }
        }
        
        public object NewValue
        {
            get
            {
                return _newValue;
            }
            set
            {
                if (_enableConversion && (value is string))
                	_newValue = Controller.StringToValue(((string)(value)));
                else
                	_newValue = value;
            }
        }
        
        public bool Modified
        {
            get
            {
                return (_modified && !(ReadOnly));
            }
            set
            {
                _modified = value;
                _noCheck = true;
            }
        }
        
        public bool ReadOnly
        {
            get
            {
                return _readOnly;
            }
            set
            {
                _readOnly = value;
            }
        }
        
        public object Value
        {
            get
            {
                CheckModified();
                if (Modified || ReadOnly)
                	return NewValue;
                else
                	return OldValue;
            }
            set
            {
                OldValue = value;
                Modified = false;
            }
        }
        
        public string Error
        {
            get
            {
                return this._error;
            }
            set
            {
                this._error = value;
            }
        }
        
        public override string ToString()
        {
            string oldValueInfo = String.Empty;
            object v = Value;
            if (this.Modified)
            {
                object ov = OldValue;
                if (ov == null)
                	ov = "null";
                oldValueInfo = String.Format(" (old value = {0})", ov);
            }
            string isReadOnly = String.Empty;
            if (ReadOnly)
            	isReadOnly = " (read-only)";
            if (v == null)
            	v = "null";
            string err = String.Empty;
            if (!(String.IsNullOrEmpty(Error)))
            	err = String.Format("; Input Error: {0}", Error);
            return String.Format(String.Format("{0} = {1}{2}{3}{4}", Name, v, oldValueInfo, isReadOnly, err));
        }
        
        public void CheckModified()
        {
            if (_noCheck)
            	return;
            if (String.Empty.Equals(NewValue))
            	NewValue = null;
            if (NewValue == null)
            	if (OldValue != null)
                	_modified = true;
                else
                	_modified = false;
            else
            	if (OldValue != null)
                	_modified = !(NewValue.Equals(OldValue));
                else
                	_modified = true;
        }
        
        public void AssignTo(object instance)
        {
            CheckModified();
            Type t = instance.GetType();
            System.Reflection.PropertyInfo propInfo = t.GetProperty(Name);
            object v = Value;
            if (v != null)
            	if (propInfo.PropertyType.IsGenericType)
                	if (propInfo.PropertyType.GetProperty("Value").PropertyType.Equals(typeof(Guid)))
                    	v = new Guid(Convert.ToString(v));
                    else
                    	v = Convert.ChangeType(v, propInfo.PropertyType.GetProperty("Value").PropertyType);
                else
                	v = Convert.ChangeType(v, propInfo.PropertyType);
            t.InvokeMember(Name, System.Reflection.BindingFlags.SetProperty, null, instance, new object[] {
                        v});
        }
        
        public void EnableConversion()
        {
            _enableConversion = true;
        }
        
        public void DisableConversion()
        {
            _enableConversion = false;
        }
    }
}
