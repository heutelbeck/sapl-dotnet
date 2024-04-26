## Project Title
Sapl ASP.NET Core Security Integration

## Introduction
Similar to ASP.NET Core Security itself, the SAPL integration supports  blocking method security annotations.
Just like in ASP.NET Core Security, method security is not active by default and one of the two modes has to be activated.
While in ASP.NET Core Security Security it is sufficient to add the attribute [Authorize] or [Authorize(PolicyName="MyPolicy")]
on any configuration class, a specific configuration class has to be provided to activate the SAPL attributes alongside the ASP.NET Core Security method security attributes.

## Table of Contents
- Introduction
- Features
- Project Structure
- License
- Installation and usage

## Features
- **Authorization Decision Handling**: Manages various authorization decisions.
- **Subscription Management**: Supports tracking of authorization subscriptions.
- **Exception Handling**: Efficiently handles unknown exceptions.
- **Notification System**: Observes and notifies on authorization decision changes.
- **PDP Integration**: Enables interaction with Policy Decision Points.
- **Demo Applications**: Includes demos for Web API, minimal web API, and SignalR integration.
- **Security Implementations**: Features ASP.NET Core Security integration.

## Project Structure
1. **PDP.Api**: The core API for handling policy decision logic.
2. **PDP.Client**: A client library for interacting with the PDP API.
3. **Web-API-Demo**: A demonstration of the API's integration in a traditional web application.
4. **Minimal-Web-API-Demo**: A minimalistic demo showcasing a lightweight implementation.
5. **SignalR-Demo**: An application showcasing real-time updates using SignalR.
6. **SAPL.AspNetCore.Security**: Security aspects specifically tailored for ASP.NET Core.
7. **Test Projects**: Unit tests and integration tests for the above components.

## License
This project is licensed under the Apache License, Version 2.0. See [LICENSE](http://www.apache.org/licenses/LICENSE-2.0) for details.

## Installation and usage

### Create a ASP.NET Core Web API with ASBAC/SAPL-Autorization

This tutorial teaches the basics of building a controller-based web API that uses no database.

#### Prerequisites

Install Visual Studio 2022 with ASP.NET and web developement

![](images/1.png)

#### Create a web project

- From the **File** menu, select **New** > **Project**.
- Enter _Web API_ in the search box.
- Select the **ASP.NET Core Web API** template and select **Next**.

![](images/2.png)

In the **Configure your new project dialog**, name the project _DemoApi_ and select **Next**.

![](images/3.png)

- In the **Additional information** dialog:
- Confirm the **Framework** is **.NET 8.0 (Long Term Support)**.
- Confirm the checkbox for **Use controllers(uncheck to use minimal APIs)** is checked.
- Confirm the checkbox for **Enable OpenAPI support** is checked.
- Select **Create**.

![](images/4.png)

#### Install the SAPL-NuGet packages

A NuGet package must be added to support the Itegration used in this install-guide.

- From the Tools menu, select NuGet Package Manager => Manage NuGet Packages for Solution
- Select the Browse tab
- Enter **PDP.Api** in the search box, and then select **PDP.Api**
- Select the Project checkbox in the right pane and then select Install
- Enter **PDP.Client** in the search box, and then select **PDP.Client**
- Select the Project checkbox in the right pane and then select Install
- Enter **SAPL.AspNetCore.Security** in the search box, and then select **SAPL.AspNetCore.Security**
- Select the Project checkbox in the right pane and then select Install

![](images/5.png)

#### Test the project

Press Ctrl+F5 to run without the debugger.

Visual Studio displays the following dialog when a project is not yet configured to use SSL:

![](images/6.png)

Select Yes if you trust the IIS Express SSL certificate.

The following dialog is displayed:

![](images/7.png)

Select Yes if you agree to trust the development certificate.

### Set connection to SAPL-Server

##### Storing the access data for SAPL-Server lt

Open the File appsettings.json add the following content and save the file.

![](images/8.png)

### Add the SAPL/ASBAC components to the request-response-pipeline

##### Add necessary namespaces to Program.cs

To register the SAPL components in the application, open the Program.cs file and add the namespaces

using SAPL.AspNetCore.Security.Authentication;

using SAPL.AspNetCore.Security.Constraints.api;

using SAPL.AspNetCore.Security.Extensions;

using SAPL.AspNetCore.Security.Middleware.Exception;

using SAPL.WebAPIDemo.ExampleData.Data;

to the head of the file.

![](images/10.png)

##### Registrate base components with bearer token autentication

To register the necessary basic components as DI services in the application, it's necessary to specify the type of authentication. Currently, Bearer Token Authentication is supported. Each request to the application requires the provision of an Authorization header containing an encrypted token. The registration can be carried out as follows.

![](images/11.png)

###### Add secret key for Bearer Token authentication

For the use of token-based authentication, it is necessary to specify a key in the appsettings.json file. For this purpose, the following content is added to the file. The key can, of course, be replaced by your own key.

![](images/12.png)

##### Register base components with custom authentication

To use custom authentication methods, an object of type ISubjectFromAuthenticationBuilder must be implemented and passed into the registration.

![](images/13.png)

##### Add SAPL-Middleware to the pipeline

To obtain a standardized error handling of access errors, it is necessary to place a middleware in the pipeline. This is also done in the Program.cs file as follows:

![](images/14.png)

##### Registrate constraint handler

The registration of the constraint handlers as DI services is also done in the Program.cs file under the specification of the interface IResponsibleConstraintHandlerProvider and takes place as follows:

![](images/15.png)

### Secure endpoints in controller based Web API

For securing multiple endpoints in a controller, it is first necessary to extend the controller with a "Route" attribute, which ensures the unique identification of an endpoint. To do this, the Route attribute is added as follows.

![](images/16.png)

#### PreEnforcement

For the use of PreEnforcement, a PreEnforce attribute is attached to the method to be secured in the respective controller. Within the attribute, parameters such as subject, action, resource, and environment can be set. If the parameters are not preset, the framework determines the attributes itself to create an authorization subscription. An example of a PreEnforce attribute specifying the subject can be seen in the following figure.

![](images/17.png)

##### Test the preenforcement

- Press Ctrl+F5 to run the app.
- In the Swagger browser window, select **GET /api/WeatherForecast/Get**, and then select **Try it out**.

![](images/18.png)

- Select Execute
- In the Response body window you see „Access denied“ because of missing policy

![](images/19.png)

- Write a new Policy like below and add it to your local PAP and try it again

![](images/20.png)

- Press Ctrl+F5 to run the app.
- In the Swagger browser window, select **GET /api/WeatherForecast/Get**, and then select **Try it out**.
- Select Execute
- In the Response body window you see the result

![](images/21.png)

#### PostEnforcement

For using PostEnforcement, similarly to PreEnforcement, a PostEnforce attribute is attached to the method to be secured in the respective controller. Within the attribute, parameters such as subject, action, resource, and environment can be set. If the parameters are not pre-defined, the framework determines the attributes itself to create an authorization subscription.

Copy the Get method and rename it to GetTwo. An example of a PostEnforce attribute specifying the subject can be seen in the following figure.

![](images/22.png)

##### Test the postenforcement

- Write a new Policy like below and add it to your local PAP

![](images/23.png)

- Press Ctrl+F5 to run the app.
- In the Swagger browser window, select **GET /api/WeatherForecast/GetTwo**, and then select **Try it out**.
- Select Execute

![](images/24.png)

- In the Response body window you see the result

![](images/25.png)

### Secure endpoints in minimal Web API

For securing endpoints in minimal Web API without controller, it is first necessary register endpointfilter in the Program.cs-file.

#### PreEnforcement

For the use of PreEnforcement, a PreEnforce-Filter hast o be registrated on the Endpoint in the Program.cs-file. An example of a registration of aPreEnforce-filter can be seen in the following figure.

![](images/26.png)

#### PostEnforcement

For the use of PostEnforcement, a PostEnforce-Filter hast o be registrated on the Endpoint in the Program.cs-file. An example of a registration of aPreEnforce-filter can be seen in the following figure.

![](images/27.png)

### Secure endpoints in SignalR

For securing endpoints in a SignalR application without controller, it is first necessary register hubfilter in the Program.cs-file.

#### PreEnforcement

For the use of PreEnforcement, a PreEnforceHub-Filter hast o be registrated on the Endpoint in the Program.cs-file. An example of a registration of a PreEnforce-filter can be seen in the following figure.

![](images/28.png)

### Using implemented constraint handler for transformations

To manipulate the results of an endpoint during PostEnforcement, three different constraint handlers are offered, which do not require implementation and are controlled by a corresponding policy. These include "blacken," "replace," and "delete," which are described in the following.

##### Blacken

To redact content from the result of an endpoint, the "type" attribute within the obligation just needs to be set to "filterJsonPathContent" in the policy, and a valid JsonPath expression must be specified as "path". For redaction, "blacken" is specified as the type of the action. The following image shows an example of a corresponding policy.

![](images/29.png)

##### Replace

To replace contents of a result from an endpoint with a specific value, the "type" attribute within the obligation must be set to "filterJsonPathContent" in the policy, and a valid JsonPath expression must be specified as "path". For the replacement, "replace" is specified as the type of the action. The following image shows an example of a corresponding policy.

![](images/30.png)

##### Delete

To delete contents of a result from an endpoint, the "type" attribute within the obligation must be set to "filterJsonPathContent" in the policy, and a valid JsonPath expression must be specified as "path". For deletion, "delete" is specified as the type of the action. The following image shows an example of a corresponding policy.

![](images/31.png)

### Implementing custom constraint handler

Here are some examples of how constraint handlers can be implemented for different purposes. For each example, the source code and a policy are illustrated.

##### Typed Filter

Um Ergebnisse eines Endpunktes nach bestimmten Kriterien, die in einer Policy enthalten sein können, zu Filtern, muss ein constrainthandler vom Typ TypedFilterPredicateConstraintHandlerProviderBase&lt;T&gt; implementiert werden. Unter der Angabe des Typs kann eine Filterung der Ergebnisse an Hand eines Prädikats vorgenommen werden. Ein Beispiel für eine Implementierung und die dazugehörende Policy sind in den folgenden Abbildungen dargestellt.

![](images/32.png)

![](images/33.png)

##### LoggingConsumerDelegateConstraintHandler

Ein constraint handler der Daten aus einer Policy in die Anwendung übergibt wird nach dem folgendem Muster implementiert. Hierbei ist das Interface IConsumerDelegateConstraintHandlerProvider&lt;string&gt; zu implementieren.

![](images/34.png)

The corresponding policy might look as follows:

![](images/35.png)

##### LoggingDelegateConstraintHandler

Constraint handlers that implement the IRunnableDelegateConstraintHandlerProvider interface do not take data from the policy and can be implemented for logging according to the following pattern.

![](images/36.png)

Or for the simpler cases:

![](images/37.png)

##### Manipulate methodinvocation

To manipulate parameters of a called method of an endpoint, the IActionExecutingContextConstraintHandlerProvider interface is implemented. An example is shown in the following figure.

![](images/38.png)

The corresponding policy might look as follows:

![](images/39.png)