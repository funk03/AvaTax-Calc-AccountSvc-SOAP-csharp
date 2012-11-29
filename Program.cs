using System;
using System.Collections.Generic;
using Microsoft.Web.Services3;
using Microsoft.Web.Services3.Security.Tokens;
using Microsoft.Web.Services3.Addressing;
using System.Text;
using System.Web.Services;
using System.Threading;
using Avalara.AvaTax.Services.Proxies.AccountSvcProxy;

namespace AccountSvcTest
{
    class Program
    {
        static void Main(string[] args)
        {
            String url = "https://development.avalara.net";
            String username = ""; //You must authenticate with username/password (and not account number/ license key)
            String password = "";                     //Also, the credentials will need to be CompanyAdmin or AccountAdmin level.

            int accountId = 1100012345; //This should be your account Id - used for company creation, not authentication. 

            AccountSvc svc = new AccountSvc(url, username, password);



            int companyId = CreateNewCompany(svc, accountId); //When a new company is created, you assign it a company code, and we assign it a companyId. I'll use that CompanyId to create users and nexus.
            AddNexus(svc, companyId);
            AddUser(svc, companyId, accountId);


            //Here are some other things that aren't in the sample, but let me know if you'd like to see them:
                //- Use merchant credentials for further editing (and allow them to reset an initial password progamatically)
                //- Add/modify Tax Codes, Tax Rules, Exemption Certificates, Users


            Console.WriteLine("Tests Done!");
            Console.ReadLine();
        }
        private static int CreateNewCompany(AccountSvc svc, int accountId) //Creates a new company in the target account with hardcoded company detail.
        {
            //check credentials by pulling current account data
            FetchRequest req = new FetchRequest();
            req.Filters = "AccountId=" + accountId;
            CompanyFetchResult fetchres = svc.CompanyFetch(req);
            Console.WriteLine("Companies fetched: "+ fetchres.RecordCount);


            Company company = new Company();
            company.AccountId = accountId;
            company.CompanyCode = "XYZ"+DateTime.Now; //This will need to be unique on the account - it should be whatever unique system value you assign to the merchant.
            company.CompanyName = "Test Company XYZ"; //Does not need to be strictly unique - should be the legal name of the merchant.

            CompanyContact company_contact = new CompanyContact(); //This should be the contact info for your primary contact at the merchant company.
            company_contact.CompanyContactCode = "001";
            company_contact.FirstName = "Anya";
            company_contact.LastName = "Stettler";
            company_contact.Line1 = "100 Ravine Lane";
            company_contact.City = "Bainbridge Island";
            company_contact.Region = "WA";
            company_contact.PostalCode = "98110";
            company_contact.Phone = "1-877-780-4848";
            company_contact.Email = "sdksupport@avalara.com";
            CompanyContact[] arr = new CompanyContact[1];
            arr[0] = company_contact;
            company.Contacts = arr;

            company.IsActive = true; // Allow us to skip activiation later.
            company.HasProfile = true; //Tells us that you will be creating a tax profile for this company instead of inheriting an existing one.
            company.IsReportingEntity = true; //Separates reported transctions from other companies.

            CompanySaveResult res = new CompanySaveResult();
            try
            {
                res = svc.CompanySave(company); //Save the company
                if (res.ResultCode.Equals(SeverityLevel.Success))
                {
                    Console.WriteLine("Company saved successfully. CompanyId: " + res.CompanyId);
                }
                else
                {
                    Console.WriteLine("Error when saving company. Error: " + res.Messages[0].Summary);

                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error in CompanySave: " + ex);
            }

            return res.CompanyId; //Return the newly-created companyId
        }
        private static void AddUser(AccountSvc svc, int companyId, int accountId) //You might want to create a company-specific user login for the company in question.
        {
            User newUser = new User();
            newUser.CompanyId = companyId;
            newUser.AccountId = accountId;

            newUser.Email = "anya.stettler@avalara.com";//Should be merchant/client user
            newUser.FirstName = "Anya";
            newUser.LastName = "Stettler";
            newUser.UserName = "sdksupport@avalara.com"+DateTime.Now; //Must be unique within the Avalara server - we usually use email address. I've added a timestamp here for testing.
            newUser.PostalCode = "98110"; //Not required, but improves user experience (as postal code is required to perform a password reset)

            newUser.SecurityRoleId = SecurityRoleId.CompanyAdmin; //This will give the user access to the specified company ONLY, and will allow them to change profile settings, etc. for that company.
            newUser.IsActive = true;
            
            UserSaveResult res = new UserSaveResult();
            try
            {
                res = svc.UserSave(newUser); //Save the user
                if (res.ResultCode.Equals(SeverityLevel.Success))
                {
                    Console.WriteLine("User saved successfully. UserId: " + res.UserId);
                }
                else
                {
                    Console.WriteLine("Error when saving user. Error: " + res.Messages[0].Summary);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error in UserSave: " + ex);
            }

            //By default, a new user is assigned a password (which is emailed to them). They are required to change this password upon first login to the admin console.
            //If you want to eliminate all contact with the Admin Console, you'll need to reset the password programatically.
            //Note: this kicks off an email to the user email address notifying them that someone has changed their account.
            newUser.UserId = res.UserId;
            newUser.PasswordStatusId = PasswordStatusId.UserCanChange; //This removes the "must change" property
            newUser.Password = "password123";
            res = svc.UserSave(newUser);
        }
        private static void AddNexus(AccountSvc svc, int companyId)
        {
            FetchRequest nexusFetch = new FetchRequest();
            nexusFetch.Filters = "CompanyId=" + companyId;

            nexusFetch.Filters = "CompanyId=1,EndDate='12/31/9999'";//Gets master list of possible nexus
            NexusFetchResult databaseNexus = svc.NexusFetch(nexusFetch);


            Nexus[] to_add = {databaseNexus.Nexuses[2], databaseNexus.Nexuses[7], databaseNexus.Nexuses[10] }; //I've just selected some values to add at random, this would be informed by your user input matched against the master list to find the 
                                                // correct jurisdiction. You should compare the Nexus.JurisName. You don't have to add US nexus to add states throught the API, it adds the country nexus automatically.

            foreach (Nexus n in to_add)
            {
                Nexus newNexus = new Nexus();
                newNexus = n;
                newNexus.CompanyId = companyId;
                newNexus.NexusId = 0; //Always set to 0 for a new nexus, otherwise use the fetched NexusId value to edit an existing nexus.
                newNexus.EffDate = DateTime.Today.AddYears(-1); //I'm setting the default start/end dates the same way the Admin Console will, user should specify.
                newNexus.EndDate = DateTime.MaxValue;
                newNexus.LocalNexusTypeId = LocalNexusTypeId.Selected; //This may vary based on the jurisdiction and needs of the client.
                NexusSaveResult res = svc.NexusSave(newNexus);
                if (res.ResultCode.Equals(SeverityLevel.Success))
                {
                    Console.WriteLine("Nexus saved for: " + newNexus.JurisName);
                }
                else{
                    Console.WriteLine("Nexus NOT saved for: " + newNexus.JurisName +
                        " //Error: " + res.Messages[0].Summary);
                }
            }


        }

        class AccountSvc : AccountSvcWse
        {
            public AccountSvc(string Url, string userName, string passWord)
            {

                this.Destination = Util.GetEndpoint(Url, "Account/AccountSvc.asmx");

                // Setup WS-Security authentication 
                UsernameToken userToken = new UsernameToken(userName, passWord, PasswordOption.SendPlainText);
                SoapContext requestContext = this.RequestSoapContext;
                requestContext.Security.Tokens.Add(userToken);
                requestContext.Security.Timestamp.TtlInSeconds = 300;

                Avalara.AvaTax.Services.Proxies.AccountSvcProxy.Profile profile = new Avalara.AvaTax.Services.Proxies.AccountSvcProxy.Profile();
                profile.Client = "AccountSvcSampleCode"; // _config.Client; 
                profile.Adapter = "";
                profile.Name = "";
                profile.Machine = System.Environment.MachineName;



                this.ProfileValue = profile;
            }
        }
        class Util
        {
            public static EndpointReference GetEndpoint(string Url, string path)
            {
                EndpointReference endpoint = null;
                string url = Url;
                string viaurl = Url;

                if (url.Trim().StartsWith("https://"))
                {
                    url = url.Trim().Replace("https://", "http://");
                }

                if (!url.EndsWith("/"))
                {

                    url += "/";

                }

                if (!viaurl.EndsWith("/"))
                {

                    viaurl += "/";

                }

                endpoint = new EndpointReference(new Uri(url + path), new Uri(viaurl + path));

                return endpoint;

            }
        }
    }
}
