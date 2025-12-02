using Projects;

var builder = DistributedApplication.CreateBuilder(args);

builder.AddProject<LikeLionChat_Server>("chat-server");

builder.Build().Run();
