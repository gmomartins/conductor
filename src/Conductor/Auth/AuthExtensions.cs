﻿using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Org.BouncyCastle.Security;
using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text.Encodings.Web;
using System.Threading.Tasks;

namespace Conductor.Auth
{
    public static class AuthExtensions
    {
        public static AuthenticationBuilder AddJwtAuth(this AuthenticationBuilder builder, IConfiguration config)
        {
            builder.AddJwtBearer(options =>
             {
                 var publicKey = Convert.FromBase64String(config.GetValue<string>("AuthPublicKey"));
                 var e1 = ECDsa.Create();
                 e1.ImportParameters(new ECParameters()
                 {
                     Curve = ECCurve.NamedCurves.nistP256,
                     Q = new ECPoint()
                     {
                         X = publicKey.Take(32).ToArray(),
                         Y = publicKey.Skip(32).Take(32).ToArray()
                     }
                 });

                 options.IncludeErrorDetails = true;

                 options.TokenValidationParameters = new TokenValidationParameters
                 {
                     ValidateIssuerSigningKey = true,
                     IssuerSigningKey = new ECDsaSecurityKey(e1),
                     ValidateIssuer = false,
                     ValidateAudience = false
                 };

                 options.RequireHttpsMetadata = false;
                 options.SaveToken = true;

                 options.Validate();
             });

            return builder;
        }

        public static AuthenticationBuilder AddBypassAuth(this AuthenticationBuilder builder)
        {
            var tokenHandler = new JwtSecurityTokenHandler();
            var securityKey = new SymmetricSecurityKey(new byte[121]);
            var sc = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);

            var tokenDescriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(new[]
                {
                    new Claim( ClaimTypes.Role, Roles.Admin),
                    new Claim( ClaimTypes.Role, Roles.Author),
                    new Claim( ClaimTypes.Role, Roles.Controller),
                    new Claim( ClaimTypes.Role, Roles.Viewer),
                }),
                SigningCredentials = sc,
            };

            var token = tokenHandler.CreateToken(tokenDescriptor);
            var tokenString = tokenHandler.WriteToken(token);

            builder.AddJwtBearer(options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuerSigningKey = false,
                    IssuerSigningKey = securityKey,
                    ValidateIssuer = false,
                    ValidateAudience = false,
                    ValidateLifetime = false,
                };
                options.RequireHttpsMetadata = false;
                options.Events = new JwtBearerEvents()
                {
                    OnMessageReceived = context =>
                    {
                        context.Token = tokenString;
                        return Task.CompletedTask;
                    }                    
                };
                options.Validate();
            });

            return builder;
        }
    }
    
}