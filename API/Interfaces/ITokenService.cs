using API.Data;

namespace API.Interfaces {
  public interface ITokenService {
    string CreateToken(User user);
  }
}
