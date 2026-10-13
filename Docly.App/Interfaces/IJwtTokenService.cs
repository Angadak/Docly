using Docly.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Text;

namespace Docly.App.Interfaces
{
    public interface IJwtTokenService
    {
        string GenerateToken(User user);
    }
}
