using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using System.Xml;
using System.Xml.Schema;
using log4net;

namespace XmlValidation
{
    public class SchemaValidator
    {
        #region Private members
        XmlSchemaSet schemaset;
        ILog logger;
        static object lockObject = new Object();
        private static object schemasByNameLockFile = new Object();
        private static Dictionary<string, XmlSchema> schemasByName = new Dictionary<string, XmlSchema>();
        StringBuilder errors, warnings;
        #endregion

        #region Properties
        /// <summary>
        /// Sets/gets property WarningAsErrors
        /// </summary>
        public bool WarningAsErrors { get; set; }
        /// <summary>
        /// Sets/gets property ErrorMessage 
        /// </summary>
        public string ErrorMessage { get; set; }
        /// <summary>
        /// Sets/gets property WarningMessage
        /// </summary>
        public string WarningMessage { get; set; }
        #endregion 

        #region CTORs
        /// <summary>
        /// Schema path constructor
        /// </summary>
        /// <param name="schemaPath"></param>
        public SchemaValidator(string schemaPath)
            : this(Directory.GetFiles(schemaPath, "*.xsd", SearchOption.AllDirectories), true)
        {
        }

        /// <summary>
        /// Schema files contructor (isFileName set to true)
        /// </summary>
        /// <param name="schemaFiles"></param>
        public SchemaValidator(string[] schemaFiles)
            : this(schemaFiles, true)
        {

        }

        /// <summary>
        /// Schema names (maybe files) constructor
        /// </summary>
        /// <param name="schemaNames"></param>
        /// <param name="isFileName"></param>
        public SchemaValidator(string[] schemaNames, bool isFileName)
        {
            WarningAsErrors = true;
            logger = LogManager.GetLogger(GetType().Name);
            schemaset = new XmlSchemaSet();
            foreach (string s in schemaNames)
            {
                lock (schemasByNameLockFile)
                {
                    XmlSchema xmlSchema;
                    if (!schemasByName.TryGetValue(s, out xmlSchema))
                    {
                        XmlReader xmlReader;
                        if (isFileName)
                            xmlReader = XmlReader.Create(s);
                        else
                            xmlReader = XmlReader.Create(GetTextReaderFromResource(s));
                        xmlSchema = XmlSchema.Read(xmlReader, new ValidationEventHandler((ss, e) => OnValidateReadSchema(ss, e)));
                        schemasByName.Add(s, xmlSchema);
                    }
                    schemaset.Add(xmlSchema);
                }
            }
        }
        #endregion

        #region Public methods
        /// <summary>
        /// Returns a string from a resource name
        /// </summary>
        /// <param name="resourceName"></param>
        /// <returns></returns>
        public static string GetStringResource(string resourceName)
        {
            string result = null;
            var assembly = Assembly.GetExecutingAssembly();
            using (Stream stream = assembly.GetManifestResourceStream(resourceName))
            {
                using (StreamReader reader = new StreamReader(stream))
                {
                    result = reader.ReadToEnd();
                }
            }
            return result;
        }        
        #endregion

        #region Private methods
        /// <summary>
        /// Returns a StringReader from a resource name
        /// </summary>
        /// <param name="resourceName"></param>
        /// <returns></returns>
        private TextReader GetTextReaderFromResource(string resourceName)
        {
            return new StringReader(GetStringResource(resourceName));
        }

        /// <summary>
        /// Formats schema exception 
        /// </summary>
        /// <param name="xmlSchemaException"></param>
        /// <returns></returns>
        private string FormatLineInfo(XmlSchemaException xmlSchemaException)
        {
            return string.Format(" Line:{0} Position:{1}", xmlSchemaException.LineNumber, xmlSchemaException.LinePosition);
        }
        #endregion

        #region Event handlers
        /// <summary>
        /// Handles validation event reading schema
        /// </summary>
        /// <param name="ss"></param>
        /// <param name="e"></param>
        private void OnValidateReadSchema(object ss, ValidationEventArgs e)
        {
            if (e.Severity == XmlSeverityType.Error)
                logger.Error(e.Message);
            else
                logger.Warn(e.Message);
        }

        /// <summary>
        /// Handles validation errors
        /// </summary>
        /// <param name="_"></param>
        /// <param name="vae"></param>
        protected void OnValidate(object _, ValidationEventArgs vae)
        {
            if (vae.Severity == XmlSeverityType.Error)
                logger.Error(vae.Message);
            else
                logger.Warn(vae.Message);
            if (vae.Severity == XmlSeverityType.Error || WarningAsErrors)
                errors.AppendLine(vae.Message + FormatLineInfo(vae.Exception));
            else
                warnings.AppendLine(vae.Message + FormatLineInfo(vae.Exception));
        }
        #endregion

        /// <summary>
        /// Validates the text in input (thread safe)
        /// </summary>
        /// <param name="doc"></param>
        public void Validate(String doc)
        {
            lock (lockObject)
            {
                errors = new StringBuilder();
                warnings = new StringBuilder();

                XmlReaderSettings settings = new XmlReaderSettings();
                settings.CloseInput = true;
                settings.ValidationEventHandler += new ValidationEventHandler((o, e) => OnValidate(o, e));  // Your callback...
                settings.ValidationType = ValidationType.Schema;
                settings.Schemas.Add(schemaset);
                settings.ValidationFlags =
                  XmlSchemaValidationFlags.ReportValidationWarnings |
                  XmlSchemaValidationFlags.ProcessIdentityConstraints |
                  XmlSchemaValidationFlags.ProcessInlineSchema |
                  XmlSchemaValidationFlags.ProcessSchemaLocation;

                // Wrap document in an XmlNodeReader and run validation on top of that
                try
                {
                    using (XmlReader validatingReader = XmlReader.Create(new StringReader(doc), settings))
                    {
                        while (validatingReader.Read()) { /* just loop through document */ }

                    }
                }
                catch (XmlException e)
                {
                    errors.AppendLine(string.Format("Errore in lettura Linea:{0} Posizione:{1}", e.LineNumber, e.LinePosition));
                }
                ErrorMessage = errors.ToString();
                WarningMessage = warnings.ToString();
            }
        }        
    }
}
