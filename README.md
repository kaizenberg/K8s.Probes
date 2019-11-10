# K8s.Probes

This sample code demonstrates how console or background service can easily achieve self-healing capability if it is containerized and hosted in Kubernetes cluster.

For demostration;

- Cross-platform .Net Core 3.0 application building framework is used to develop a worker service that runs continuously and processes some long running jobs that it picks up by itself.
- App is containerized using Docker Linux VM
- It is then hosted on Azure inside Azure Kubernetes Service (AKS) cluster.
- App relies of external service such as two Azure Service Bus Queues.
- App must not start processing jobs unless it ensures that its dependencies are accessible and hence available. This is a readiness check.
- After successful validtion it should periodically send some sort of health becon to indicate that is it runnning and not frozen or crashed. This is a liveliness check.

You can read about Kubernetes Readiness & Liveliness probes. Then read below article to understand how the sample is designed & implemented. (Some work is still in progress)

https://kaizenberglabs.wordpress.com/2019/10/28/kubernetes-essentials-readiness-liveliness-probes/

Other 3rd party libraries used for demonstration are: 
- Polly (Retry Policy)
- Lamar (IoC) 
- NLog (Logging)
