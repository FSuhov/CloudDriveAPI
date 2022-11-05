using Amazon.DynamoDBv2.DataModel;

namespace API.Data {

  [DynamoDBTable("ab-clouddrive-users")]
  public class User {

    [DynamoDBHashKey("userName")]
    public string UserName { get; set; }

    [DynamoDBProperty]
    public string Password { get; set; }

    [DynamoDBProperty]
    public string Salt { get; set; }  
  }
}
