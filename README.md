# K8s.Probes

This sample code demonstrates how console or background service can easily achieve self-healing capability if it is containerized and hosted in Kubernetes cluster.

<h2>Use case</h2>

- Cross-platform .Net Core 3.0 application building framework is used to develop a worker service that runs continuously and processes some long running jobs that it reads from a Request message queue and writes the result to Response message queue.
- App is containerized using Docker Linux VM and saved to a private Azure Container Registry (ACR).
- It is then hosted on Azure inside Azure Kubernetes Service (AKS) cluster.
- App relies on an external services such as two Azure Service Bus Queues.
- App must not start processing jobs until it ensures that all of its dependencies are accessible and hence available. This is readiness check.
- After successful validtion it should periodically report that it is runnning and not frozen or crashed. This is a liveliness check.

You can read about Kubernetes Readiness & Liveliness probes. Then read below article to understand how this sample is designed & implemented. (Some work is still in progress)

https://kaizenberglabs.wordpress.com/2019/10/28/kubernetes-essentials-readiness-liveliness-probes/

Other 3rd party libraries used for demonstration purpose are: 
- Polly (Retry Policy)
- Lamar (IoC) 
- NLog (Logging)

<h2>Running the app</h2>

- Clone the repository to developer desktop
- Download & install Azure CLI
- Open PowerShell
- Login to Azure account from developer desktop</br>
<code> az login </code>
- Install Kubernetes CLI</br>
<code >az aks install-cli </code>
- Login to Azure Container Registry</br>
<code> docker login <azurecontainerregistryname> -u <username> -p <password ></code>
- Navigate to the source code
- Build Dockerfile of this project & tag it</br>
<code> docker build -f Dockerfile -t <azurecontainerregistryname>/k8s-probes-test:1.0.0 . </code>
- Push the image to ACR</br>
<code> docker push <azurecontainerregistryname>/k8s-probes-test:1.0.0 </code>
- Login to Azure Kubernetes Service cluster</br>
<code> az aks get-credentials --resource-group <resourcegroupofakscluster> --name <aksclustername> </code>
- Deploy Probes.yaml to AKS cluster</br>
<code> kubectl apply -f Probes.yaml --record </code>
- View all pods that are created and running</br>
<code> kubect get pods </code>
- View output of one of the pod</br>
<code> kubectl logs -f <id of a pod> </code>
