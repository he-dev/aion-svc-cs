using System;
using Aion.Util;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Aion.Tests;

// ReSharper disable once ClassNeverInstantiated.Global
public sealed class TestWebApplication : WebApplicationFactory<Program>;

