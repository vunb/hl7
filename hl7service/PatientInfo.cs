using System;

using System.Data.SqlClient;
using System.Collections;
using System.Collections.Specialized;

using System.Xml;

namespace hl7service
{
	public class PatientInfo
	{
		public string connectionString;
		
		// SQL Patient info
		
		public string table = string.Empty;

		// HL7 v2 Patient info
		
		protected string PATIENT_ID = "PID";
		protected string FIELD_SEPARATOR = "|";

		protected StringDictionary hl7v2 = new StringDictionary();

		protected string [] hl7v2Keys = new string [] {
			"PID",
			"PatientID","ExternalID","InternalID","AlternatePatientID","PatientName","MothersMaidenName",
			"DateTimeofBirth","Sex","PatientAlias","Race","PatientAddress","CountyCode","PhoneNumberHome",
			"PhoneNumberBusiness","PrimaryLanguage","MaritalStatus","Religion","PatientAccountNumber",
			"SSNNumber","DriversLicenseNumber","MothersIdentifier","EthnicGroup","BirthPlace",
			"MultipleBirthIndicator","BirthOrder","Citizenship","VeteransMilitaryStatus",
			"Nationality","PatientDeathDateTime","PatientDeathIndicator" };
		
		protected StringDictionary hl7v3 = new StringDictionary();
		
		protected string [] hl7v3Keys = new string [] {
			"classCode", "determinerCode", "id", "given", "family", "administrativeGenderCode", "birthTime",
			"deceasedInd", "deceasedTime", "multipleBirthInd", "multipleBirthOrderNumber", "organDonorInd",
			"maritalStatusCode", "educationLevelCode", "disabilityCode", "livingArrangementCode",
			"religiousAffiliationCode",	"raceCode", "ethnicGroupCode"};
		
		protected StringDictionary sql = new StringDictionary();
					
		protected string [] sqlKeys = new string [] {
			"Tipo","Referencia","Nombre","Nombre1","Apellido1","Apellido2","NHC",
			"Field1","Field2","Field3","Field4","Field5","Field6","Field7","Field8",
			"Field9","Field10","Alta" };
		
		protected string [] field_keys = new string [] {
			"Field1","Field2","Field3","Field4","Field5","Field6","Field7","Field8","Field9","Field10" };
				
		public PatientInfo()
		{
			SQLServerInfo sqlInfo = new SQLServerInfo();
			
			this.connectionString = sqlInfo.connectionString;
			this.table = sqlInfo.table;
			
			hl7v2.Clear();
		}
		
		public bool fromHL7v2toSQL(string text)
		{
			// Parse HL7 v2 Message
			
			// search for PID field
			int first = text.IndexOf(PATIENT_ID);
			
			if (first != -1)
			{
				hl7v2.Clear();
				
				// Read patient info
				
				string [] hl7 = text.Substring(first).Split(new Char [] {'|'});
				
				for (int index = 0; index < hl7v2Keys.Length; index++)
				{
					hl7v2.Add(hl7v2Keys[index], hl7[index]);
				}
			}
			
			// Now convert it to SQL
			sql.Clear();
			
			sql.Add("Tipo","2");
			
			sql.Add("Referencia", "NULL");

			// Calculem quins seran els camps Nombre1, Apellido1 i Apellido2
			
			string fullName = hl7v2["PatientName"];
			
			// separem per ^. Primer ve el 1r cognom i després el nom.
			string [] split = fullName.Split(new Char [] {'^'});
			
			sql.Add("Apellido1", split[0]);
			sql.Add("Apellido2", "NULL");
			
			if (hl7v2.ContainsKey("MothersMaidenName"))
			{
				sql["Apellido2"] = hl7v2["MothersMaidenName"];
			}

			// La i comença a 1 expressament en el for perquè el primer "split" és el 1r cognom
			sql.Add("Nombre1", string.Empty);
			for (int i=1; i<split.Length; i++)
			{
				sql["Nombre1"] += split[i] + " ";
			}
			
			// Nombre = Apellido1 Apellido2, Nombre1		
			if (sql["Apellido2"] != "NULL")
			{
				sql.Add("Nombre", sql["Apellido1"] + " " + sql["Apellido2"] + "," + sql["Nombre1"]);
			}
			else
			{
				sql.Add("Nombre", sql["Apellido1"] + "," + sql["Nombre1"]);
			}
			
			// NHC
			sql.Add("NHC", "NULL");
			if (hl7v2.ContainsKey("InternalID"))
			{
				sql["NHC"] = hl7v2["InternalID"];
			}
			
			// Field fields (not used)
			
			foreach (string key in field_keys)
			{
				sql.Add(key, "NULL");
			}
			
			// Alta
			
			DateTime today = DateTime.Today;

			sql.Add("Alta", today.ToString("yyyyMMdd"));
			
			return storeSQL();
		}
		
		protected bool storeSQL()
		{
			// Logger.Debug("PatientInfo connection string: " + this.connectionString);	
					
			// Create SQL String
			// Tipo Referencia Nombre Nombre1 Apellido1 Apellido2 NHC Field1 Field2 Field3 Field4 Field5 Field6 Field7 Field8 Field9 Field10 Alta			toSQL();
			
			string sqlString = "INSERT INTO SCAPersona (";

			foreach (string key in sqlKeys)
			{
				sqlString += key + ", ";
			}

			sqlString += ") VALUES (";

			if (sql["ALTA"] == "NULL") // Alta can't be NULL
			{
				sql["ALTA"] = DateTime.Today.ToString("yyyyMMdd");
			}
			
			foreach (string key in sqlKeys)
			{
				if (sql[key] == "NULL")
				{
					sqlString += "NULL,";
				}
				else
				{
					sqlString += "'" + sql[key] + "',";
				}
			}

			// treu la coma final que s'ha afegit de més.
			sqlString = sqlString.TrimEnd(new Char [] {','});
			
			sqlString += ");";
			
			Logger.Debug("SQL Command: " + sqlString);			

			SqlConnection myConnection = new SqlConnection();		
			myConnection.ConnectionString = this.connectionString;
			
			bool allOk = true;

			try 
			{
				myConnection.Open();
				
				SqlCommand myCmd = new SqlCommand(sqlString, myConnection);
				
				myCmd.ExecuteNonQuery();
			}
			catch(Exception e)
			{
				allOk = false;
				Logger.Fatal("Can't open connection to database server: " + e.ToString());
			}
			
			return allOk;
		}
		
		public void fromCSVtoSQL(string text, char csv_field_delimiter)
		{
			string [] lines = text.Split(new Char [] {'\n'});
			
			foreach (string line in lines)
			{
				if (line.Length > 0)
				{
					// Logger.Debug(line);
	
					// Hem de recòrrer la línia. El caràcter csv_field_delimiter separa els camps
					// però s'ha de vigilar perquè aquest mateix caràcter pot estar dins d'un string.
					
					sql.Clear();
					
					int i = 0, k = 0;
					bool insideString = false;
					string field = string.Empty;
								
					while (i < line.Length)
					{
						if (line[i] == '\"')
						{
							insideString = !insideString;
						}
						else if (line[i] == csv_field_delimiter && !insideString)
						{
							sql.Add(sqlKeys[k++], field);
							field = string.Empty;
						}
						else
						{
							field += line[i];
						}

						i++;
					}
	
					storeSQL();
				}
			}
		}

		public void fromTXTtoSQL(string text)
		{
			string [] lines = text.Split(new Char [] {'\n'});
			
			foreach (string line in lines)
			{
				if (line.Length > 0)
				{
					// Logger.Debug("Line: " + line);
					
					// each field is separated by a tab char
					string [] split = line.Split(new Char [] {'\t'});
					
					// Convert it to SQL
					sql.Clear();
					
					int keyIndex = 0;
					
					foreach(string s in split)
					{
						sql.Add(sqlKeys[keyIndex++], s);
					}		
				
					storeSQL();
				}
			}			
		}
		
		public bool fromHL7v3toSQL(string xml)
		{
			XmlTextReader reader = new XmlTextReader(new System.IO.StringReader(xml));
			
			hl7v3.Clear();
			
			foreach (string key in hl7v3Keys)
			{
				hl7v3.Add(key, "NULL");				
			}
			
            while (reader.Read())
            {
				if (reader.NodeType == XmlNodeType.Element)
				{
					if (hl7v3.ContainsKey(reader.Name))
					{
						if (reader.HasValue)
						{
							hl7v3[reader.Name] = reader.Value;
						}
						else
						{
							hl7v3[reader.Name] = reader.GetAttribute("value");
						}
					}
				}
			}
			
			// Get SQL Info from HL7v3
		
			/*
			// Now convert it to SQL
			sql.Clear();
			
			sql.Add("Tipo","2");
			
			sql.Add("Referencia", "NULL");

			// Calculem quins seran els camps Nombre1, Apellido1 i Apellido2
			
			string fullName = hl7v2["PatientName"];
			
			// separem per ^. Primer ve el 1r cognom i després el nom.
			string [] split = fullName.Split(new Char [] {'^'});
			
			sql.Add("Apellido1", split[0]);
			sql.Add("Apellido2", "NULL");
			
			if (hl7v2.ContainsKey("MothersMaidenName"))
			{
				sql["Apellido2"] = hl7v2["MothersMaidenName"];
			}

			// La i comença a 1 expressament en el for perquè el primer "split" és el 1r cognom
			sql.Add("Nombre1", string.Empty);
			for (int i=1; i<split.Length; i++)
			{
				sql["Nombre1"] += split[i] + " ";
			}
			
			// Nombre = Apellido1 Apellido2, Nombre1		
			if (sql["Apellido2"] != "NULL")
			{
				sql.Add("Nombre", sql["Apellido1"] + " " + sql["Apellido2"] + "," + sql["Nombre1"]);
			}
			else
			{
				sql.Add("Nombre", sql["Apellido1"] + "," + sql["Nombre1"]);
			}
			
			// NHC
			sql.Add("NHC", "NULL");
			if (hl7v2.ContainsKey("InternalID"))
			{
				sql["NHC"] = hl7v2["InternalID"];
			}
			
			// Field fields (not used)
			
			foreach (string key in field_keys)
			{
				sql.Add(key, "NULL");
			}
			
			// Alta
			
			DateTime today = DateTime.Today;

			sql.Add("Alta", today.ToString("yyyyMMdd"));
			
			return storeSQL();
			 */
			return false;
		}
	}
}

