﻿/*
 * Crown Copyright © Department for Education (UK) 2016
 * 
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 * 
 *     http://www.apache.org/licenses/LICENSE-2.0
 * 
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */

using log4net;
using Sif.Framework.Model.Infrastructure;
using Sif.Framework.Model.Responses;
using Sif.Framework.Service.Mapper;
using Sif.Framework.Service.Registration;
using Sif.Framework.Service.Serialisation;
using Sif.Framework.Utils;
using Sif.Specification.Infrastructure;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Web.Http;
using Environment = Sif.Framework.Model.Infrastructure.Environment;

namespace Sif.Framework.Consumers
{
    /// <summary>
    /// The base class for all Functional Service consumers
    /// </summary>
    public class FunctionalServiceConsumer
    {
        private static readonly ILog log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private Environment environmentTemplate;
        private RegistrationService registrationService;


        /// <summary>
        /// Consumer environment template
        /// </summary>
        protected Environment EnvironmentTemplate
        {
            get { return environmentTemplate; }
        }

        /// <summary>
        /// Current consumer environment.
        /// </summary>
        protected Environment Environment { get; private set; }

        /// <summary>
        /// Service for Consumer registration.
        /// </summary>
        protected RegistrationService RegistrationService
        {
            get { return registrationService; }
        }

        /// <summary>
        /// Create a Consumer instance based upon the Environment passed.
        /// </summary>
        /// <param name="environment">Environment object.</param>
        public FunctionalServiceConsumer(Environment environment)
        {
            environmentTemplate = EnvironmentUtils.MergeWithSettings(environment, SettingsManager.ConsumerSettings);
            registrationService = new RegistrationService(SettingsManager.ConsumerSettings, SessionsManager.ConsumerSessionService);
        }

        /// <summary>
        /// Create a Consumer instance identified by the parameters passed.
        /// </summary>
        /// <param name="applicationKey">Application key.</param>
        /// <param name="instanceId">Instance ID.</param>
        /// <param name="userToken">User token.</param>
        /// <param name="solutionId">Solution ID.</param>
        public FunctionalServiceConsumer(string applicationKey, string instanceId = null, string userToken = null, string solutionId = null): this(new Environment(applicationKey, instanceId, userToken, solutionId))
        {
        }

        /// <summary>
        /// Convenience method to check if the Consumer is registered, throwing a standardised invalid operation exception if not.
        /// </summary>
        /// <exception cref="InvalidOperationException"/>
        protected virtual void checkRegistered()
        {
            if (!RegistrationService.Registered)
            {
                throw new InvalidOperationException("Consumer has not registered.");
            }
        }

        /// <summary>
        /// Serialise a single job entity.
        /// </summary>
        /// <param name="job">Payload of a single job.</param>
        /// <returns>XML string representation of the single job.</returns>
        public virtual string SerialiseSingle(Job job)
        {
            jobType data = MapperFactory.CreateInstance<Job, jobType>(job);
            return SerialiserFactory.GetXmlSerialiser<jobType>().Serialise(data);
        }

        /// <summary>
        /// Serialise an entity of multiple jobs.
        /// </summary>
        /// <param name="job">Payload of multiple jobs.</param>
        /// <returns>XML string representation of the multiple jobs.</returns>
        public virtual string SerialiseMultiple(IEnumerable<Job> job)
        {
            List<jobType> data = MapperFactory.CreateInstances<Job, jobType>(job).ToList();
            return SerialiserFactory.GetXmlSerialiser<List<jobType>>().Serialise(data);
        }

        /// <summary>
        /// Deserialise a single job entity.
        /// </summary>
        /// <param name="payload">Payload of a single job.</param>
        /// <returns>Entity representing the single job.</returns>
        public virtual Job DeserialiseSingle(string payload)
        {
            jobType data = SerialiserFactory.GetXmlSerialiser<jobType>().Deserialise(payload);
            return MapperFactory.CreateInstance<jobType, Job>(data);
        }

        /// <summary>
        /// Deserialise an entity of multiple jobs.
        /// </summary>
        /// <param name="payload">Payload of multiple jobs.</param>
        /// <returns>Entity representing multiple jobs.</returns>
        public virtual List<Job> DeserialiseMultiple(string payload)
        {
            List<jobType> data = SerialiserFactory.GetXmlSerialiser<List<jobType>>().Deserialise(payload);
            return MapperFactory.CreateInstances<jobType, Job>(data).ToList();
        }

        /// <summary>
        /// Register this Consumer.
        /// </summary>
        public void Register()
        {
            Environment = registrationService.Register(ref environmentTemplate);
        }

        /// <summary>
        /// Unregister this Consumer.
        /// </summary>
        /// <param name="deleteOnUnregister"></param>
        public void Unregister(bool? deleteOnUnregister = null)
        {
            registrationService.Unregister(deleteOnUnregister);
            Environment = null;
        }

        /// <summary>
        /// Create a single Job with the defaults provided, and persist it to the data store
        /// </summary>
        /// <param name="job">Job object with defaults to use when creating the Job</param>
        /// <param name="zone">The zone in which to create the Job</param>
        /// <param name="context">The context in which to create the Job</param>
        /// <returns>The created Job object</returns>
        public virtual Job Create(Job job, string zone = null, string context = null)
        {
            checkRegistered();
            
            checkJob(job, RightType.CREATE, zone);

            string url = EnvironmentUtils.ParseServiceUrl(EnvironmentTemplate, ServiceType.FUNCTIONAL) + "/" + job.Name + "s" + "/" + job.Name + HttpUtils.MatrixParameters(zone, context);
            string body = SerialiseSingle(job);
            string xml = HttpUtils.PostRequest(url, RegistrationService.AuthorisationToken, body);
            if (log.IsDebugEnabled) log.Debug("XML from POST request ...");
            if (log.IsDebugEnabled) log.Debug(xml);

            return DeserialiseSingle(xml);
        }

        /// <summary>
        /// Create a multiple Jobs with the defaults provided, and persist it to the data store
        /// </summary>
        /// <param name="jobs">Job objects with defaults to use when creating the Jobs</param>
        /// <param name="zone">The zone in which to create the Jobs</param>
        /// <param name="context">The context in which to create the Jobs</param>
        /// <returns>The created Job objects</returns>
        public virtual MultipleCreateResponse Create(List<Job> jobs, string zone = null, string context = null)
        {
            checkRegistered();

            string jobName = checkJobs(jobs, RightType.CREATE, zone);

            string url = EnvironmentUtils.ParseServiceUrl(EnvironmentTemplate, ServiceType.FUNCTIONAL) + "/" + jobName + "s" + HttpUtils.MatrixParameters(zone, context);
            string body = SerialiseMultiple(jobs);
            string xml = HttpUtils.PostRequest(url, RegistrationService.AuthorisationToken, body);
            if (log.IsDebugEnabled) log.Debug("XML from POST request ...");
            if (log.IsDebugEnabled) log.Debug(xml);
            createResponseType createResponseType = SerialiserFactory.GetXmlSerialiser<createResponseType>().Deserialise(xml);
            MultipleCreateResponse createResponse = MapperFactory.CreateInstance<createResponseType, MultipleCreateResponse>(createResponseType);

            return createResponse;
        }

        /// <summary>
        /// Get a single Job by its RefId
        /// </summary>
        /// <param name="id">The RefId of the Job to fetch</param>
        /// <param name="zone">The zone in which to operate</param>
        /// <param name="context">The context in which to operate</param>
        /// <returns>The Job object</returns>
        public virtual Job Query(Job job, string zone = null, string context = null)
        {
            checkRegistered();

            checkJob(job, RightType.QUERY, zone);

            try
            {
                string url = EnvironmentUtils.ParseServiceUrl(EnvironmentTemplate, ServiceType.FUNCTIONAL) + "/" + job.Name + "s" + "/" + job.Id + HttpUtils.MatrixParameters(zone, context);
                string xml = HttpUtils.GetRequest(url, RegistrationService.AuthorisationToken);
                if (log.IsDebugEnabled) log.Debug("XML from GET request ...");
                if (log.IsDebugEnabled) log.Debug(xml);
                return DeserialiseSingle(xml);
            }
            catch (WebException ex)
            {
                if (WebExceptionStatus.ProtocolError.Equals(ex.Status) && ex.Response != null)
                {
                    HttpStatusCode statusCode = ((HttpWebResponse)ex.Response).StatusCode;
                    if (!HttpStatusCode.NotFound.Equals(statusCode))
                    {
                        throw;
                    }
                }
                else
                {
                    throw;
                }
            }
            catch (Exception)
            {
                throw;
            }

            return null;
        }

        /// <summary>
        /// Get a all Jobs
        /// </summary>
        /// <param name="navigationPage">The page to fetch</param>
        /// <param name="navigationPageSize">The number of items to fetch per page</param>
        /// <param name="zone">The zone in which to operate</param>
        /// <param name="context">The context in which to operate</param>
        /// <returns>A page of Job objects</returns>
        public virtual List<Job> Query(string serviceName, uint? navigationPage = null, uint? navigationPageSize = null, string zone = null, string context = null)
        {
            checkRegistered();

            checkJob(new Job(serviceName), RightType.QUERY, zone, true);

            string url = EnvironmentUtils.ParseServiceUrl(EnvironmentTemplate, ServiceType.FUNCTIONAL) + "/" + serviceName + "s" + HttpUtils.MatrixParameters(zone, context);
            string xml;

            if (navigationPage.HasValue && navigationPageSize.HasValue)
            {
                xml = HttpUtils.GetRequest(url, RegistrationService.AuthorisationToken, (int)navigationPage, (int)navigationPageSize);
            }
            else
            {
                xml = HttpUtils.GetRequest(url, RegistrationService.AuthorisationToken);
            }

            return DeserialiseMultiple(xml);
        }

        /// <summary>
        /// Get a all Jobs that match the example provided.
        /// </summary>
        /// <param name="job">The example object to match against</param>
        /// <param name="navigationPage">The page to fetch</param>
        /// <param name="navigationPageSize">The number of items to fetch per page</param>
        /// <param name="zone">The zone in which to operate</param>
        /// <param name="context">The context in which to operate</param>
        /// <returns>A page of Job objects</returns>
        public virtual List<Job> QueryByExample(Job job, uint? navigationPage = null, uint? navigationPageSize = null, string zone = null, string context = null)
        {
            checkRegistered();

            checkJob(job, RightType.QUERY, zone);

            string url = EnvironmentUtils.ParseServiceUrl(EnvironmentTemplate, ServiceType.FUNCTIONAL) + "/" + job.Name + "s" + HttpUtils.MatrixParameters(zone, context);
            string body = SerialiseSingle(job);
            // TODO: Update PostRequest to accept paging parameters.
            string xml = HttpUtils.PostRequest(url, RegistrationService.AuthorisationToken, body, "GET");
            if (log.IsDebugEnabled) log.Debug("XML from POST (Query by Example) request ...");
            if (log.IsDebugEnabled) log.Debug(xml);

            return DeserialiseMultiple(xml);
        }


        /// <summary>
        /// Update single job object is not supported for Functional Services. Throws a HttpResponseException with Forbidden status code.
        /// </summary>
        /// <param name="job">Job object to update</param>
        /// <param name="zone">The zone in which to update the Job</param>
        /// <param name="context">The context in which to update the Job</param>
        public virtual void Update(Job job, string zone = null, string context = null)
        {
            checkRegistered();

            checkJob(job, RightType.UPDATE, zone);

            throw new HttpResponseException(HttpStatusCode.Forbidden);
        }

        /// <summary>
        /// Update multiple job objects is not supported for Functional Services. Throws a HttpResponseException with Forbidden status code.
        /// </summary>
        /// <param name="jobs">Job objects to update</param>
        /// <param name="zone">The zone in which to update the Jobs</param>
        /// <param name="context">The context in which to update the Jobs</param>
        public virtual MultipleUpdateResponse Update(List<Job> jobs, string zone = null, string context = null)
        {
            checkRegistered();

            checkJobs(jobs, RightType.UPDATE, zone);

            throw new HttpResponseException(HttpStatusCode.Forbidden);
        }

        /// <summary>
        /// Delete a Job object by its RefId
        /// </summary>
        /// <param name="id">The RefId of the Job to delete</param>
        /// <param name="zone">The zone in which to delete the Job</param>
        /// <param name="context">The context in which to delete the Job</param>
        public virtual void Delete(Job job, string zone = null, string context = null)
        {
            checkRegistered();

            checkJob(job, RightType.DELETE, zone);

            string url = EnvironmentUtils.ParseServiceUrl(EnvironmentTemplate, ServiceType.FUNCTIONAL) + "/" + job.Name + "s" + "/" + job.Id + HttpUtils.MatrixParameters(zone, context);
            string xml = HttpUtils.DeleteRequest(url, RegistrationService.AuthorisationToken);
            if (log.IsDebugEnabled) log.Debug("XML from DELETE request ...");
            if (log.IsDebugEnabled) log.Debug(xml);
        }

        /// <summary>
        /// Delete a series of Job objects by their RefIds
        /// </summary>
        /// <param name="ids">The RefIds of the Jobs to delete</param>
        /// <param name="zone">The zone in which to delete the Jobs</param>
        /// <param name="context">The context in which to delete the Jobs</param>
        /// <returns>A response</returns>
        public virtual MultipleDeleteResponse Delete(List<Job> jobs, string zone = null, string context = null)
        {
            checkRegistered();

            string jobName = checkJobs(jobs, RightType.DELETE, zone);

            List<deleteIdType> deleteIds = new List<deleteIdType>();

            foreach (Job job in jobs)
            {
                deleteIds.Add(new deleteIdType { id = job.Id.ToString() });
            }

            deleteRequestType request = new deleteRequestType { deletes = deleteIds.ToArray() };
            string url = EnvironmentUtils.ParseServiceUrl(EnvironmentTemplate, ServiceType.FUNCTIONAL) + "/" + jobName + "s" + HttpUtils.MatrixParameters(zone, context);
            string body = SerialiserFactory.GetXmlSerialiser<deleteRequestType>().Serialise(request);
            string xml = HttpUtils.PutRequest(url, RegistrationService.AuthorisationToken, body, "DELETE");
            if (log.IsDebugEnabled) log.Debug("XML from PUT (DELETE) request ...");
            if (log.IsDebugEnabled) log.Debug(xml);
            deleteResponseType updateResponseType = SerialiserFactory.GetXmlSerialiser<deleteResponseType>().Deserialise(xml);
            MultipleDeleteResponse updateResponse = MapperFactory.CreateInstance<deleteResponseType, MultipleDeleteResponse>(updateResponseType);

            return updateResponse;
        }

        /// <summary>
        /// Send a create operation to a specified phase on the specified job.
        /// </summary>
        /// <param name="job">The Job on which to execute the phase</param>
        /// <param name="phaseName">The name of the phase</param>
        /// <param name="body">The payload to send to the phase</param>
        /// <param name="zone">The zone in which to operate</param>
        /// <param name="context">The context in which to operate</param>
        /// <param name="contentTypeOverride">The mime type of the data to be sent</param>
        /// <param name="acceptOverride">The expected mime type of the result</param>
        /// <returns>A string, possibly containing a serialized object, returned from the functional service</returns>
        public virtual string CreateToPhase(Job job, string phaseName, string body = null, string zone = null, string context = null, string contentTypeOverride = null, string acceptOverride = null)
        {
            checkRegistered();

            checkJob(job, zone);

            string response = null;
            string url = EnvironmentUtils.ParseServiceUrl(EnvironmentTemplate, ServiceType.FUNCTIONAL) + "/" + job.Name + "s" + "/" + job.Id + "/phases/" + phaseName + HttpUtils.MatrixParameters(zone, context);
            response = HttpUtils.PostRequest(url, RegistrationService.AuthorisationToken, body, contentTypeOverride: contentTypeOverride, acceptOverride: acceptOverride);
            if (log.IsDebugEnabled) log.Debug("String from CREATE request to phase ...");
            if (log.IsDebugEnabled) log.Debug(response);
            return response;
        }

        /// <summary>
        /// Send a retrieve operation to a specified phase on the specified job.
        /// </summary>
        /// <param name="job">The Job on which to execute the phase</param>
        /// <param name="phaseName">The name of the phase</param>
        /// <param name="body">The payload to send to the phase</param>
        /// <param name="zone">The zone in which to operate</param>
        /// <param name="context">The context in which to operate</param>
        /// <param name="contentTypeOverride">The mime type of the data to be sent</param>
        /// <param name="acceptOverride">The expected mime type of the result</param>
        /// <returns>A string, possibly containing a serialized object, returned from the functional service</returns>
        public virtual string RetrieveToPhase(Job job, string phaseName, string body = null, string zone = null, string context = null, string contentTypeOverride = null, string acceptOverride = null)
        {
            checkRegistered();

            checkJob(job, zone);

            string response = null;
            string url = EnvironmentUtils.ParseServiceUrl(EnvironmentTemplate, ServiceType.FUNCTIONAL) + "/" + job.Name + "s" + "/" + job.Id + "/phases/" + phaseName + HttpUtils.MatrixParameters(zone, context);
            response = HttpUtils.PostRequest(url, RegistrationService.AuthorisationToken, body, "GET", contentTypeOverride, acceptOverride);
            if (log.IsDebugEnabled) log.Debug("String from GET request to phase ...");
            if (log.IsDebugEnabled) log.Debug(response);
            return response;
        }

        /// <summary>
        /// Send a update operation to a specified phase on the specified job.
        /// </summary>
        /// <param name="job">The Job on which to execute the phase</param>
        /// <param name="phaseName">The name of the phase</param>
        /// <param name="body">The payload to send to the phase</param>
        /// <param name="zone">The zone in which to operate</param>
        /// <param name="context">The context in which to operate</param>
        /// <param name="contentTypeOverride">The mime type of the data to be sent</param>
        /// <param name="acceptOverride">The expected mime type of the result</param>
        /// <returns>A string, possibly containing a serialized object, returned from the functional service</returns>
        public virtual string UpdateToPhase(Job job, string phaseName, string body, string zone = null, string context = null, string contentTypeOverride = null, string acceptOverride = null)
        {
            checkRegistered();

            checkJob(job, zone);
            
            string response = null;
            string url = EnvironmentUtils.ParseServiceUrl(EnvironmentTemplate, ServiceType.FUNCTIONAL) + "/" + job.Name + "s" + "/" + job.Id + "/phases/" + phaseName + HttpUtils.MatrixParameters(zone, context);
            response = HttpUtils.PutRequest(url, RegistrationService.AuthorisationToken, body, contentTypeOverride: contentTypeOverride, acceptOverride: acceptOverride);
            if (log.IsDebugEnabled) log.Debug("String from PUT request to phase ...");
            if (log.IsDebugEnabled) log.Debug(response);
            return response;
        }

        /// <summary>
        /// Send a delete operation to a specified phase on the specified job.
        /// </summary>
        /// <param name="job">The Job on which to execute the phase</param>
        /// <param name="phaseName">The name of the phase</param>
        /// <param name="body">The payload to send to the phase</param>
        /// <param name="zone">The zone in which to operate</param>
        /// <param name="context">The context in which to operate</param>
        /// <param name="contentTypeOverride">The mime type of the data to be sent</param>
        /// <param name="acceptOverride">The expected mime type of the result</param>
        /// <returns>A string, possibly containing a serialized object, returned from the functional service</returns>
        public virtual string DeleteToPhase(Job job, string phaseName, string body, string zone = null, string context = null, string contentTypeOverride = null, string acceptOverride = null)
        {
            checkRegistered();

            checkJob(job, zone);
            
            string response = null;
            string url = EnvironmentUtils.ParseServiceUrl(EnvironmentTemplate, ServiceType.FUNCTIONAL) + "/" + job.Name + "s" + "/" + job.Id + "/phases/" + phaseName + HttpUtils.MatrixParameters(zone, context);
            response = HttpUtils.DeleteRequest(url, RegistrationService.AuthorisationToken, body, contentTypeOverride: contentTypeOverride, acceptOverride: acceptOverride);
            if (log.IsDebugEnabled) log.Debug("String from DELETE request to phase ...");
            if (log.IsDebugEnabled) log.Debug(response);
            return response;
        }

        private Model.Infrastructure.Service checkJob(Job job, string zone = null)
        {
            if (job == null)
            {
                throw new ArgumentException("Job cannot be null.");
            }

            if (StringUtils.IsEmpty(job.Name))
            {
                throw new ArgumentException("Job name must be specified.");
            }
            
            Model.Infrastructure.Service service = ZoneUtils.GetService(EnvironmentUtils.GetTargetZone(Environment, zone), job.Name + "s", ServiceType.FUNCTIONAL);

            if (service == null)
            {
                throw new ArgumentException("A FUNCTIONAL service with the name " + job.Name + "s cannot be found in the current environment");
            }

            return service;
        }

        private void checkJob(Job job, RightType right, string zone = null, Boolean ignoreId= false)
        {
            Model.Infrastructure.Service service = checkJob(job, zone);

            if(!ignoreId && !right.Equals(RightType.CREATE) && job.Id == null)
            {
                throw new ArgumentException("Job must have an Id for any non-creation operation");
            }
            
            if(service.Rights[right.ToString()].Value.Equals(RightValue.REJECTED.ToString()))
            {
                throw new ArgumentException("The attempted operation is not permitted in the ACL of the current environment");
            }
        }

        private string checkJobs(IList<Job> jobs, RightType right, string zone = null)
        {
            if (jobs == null || jobs.Count == 0)
            {
                throw new ArgumentException("List of job objects cannot be null or empty");
            }

            string name = null;
            foreach (Job job in jobs)
            {
                checkJob(job, right, zone);

                if (StringUtils.IsEmpty(name))
                {
                    name = job.Name;
                }

                if (!name.Equals(job.Name))
                {
                    throw new ArgumentException("All job objects must have the same name");
                }
            }
            return name;
        }
    }
}