# Violation Patterns Reference

## Dependency Rule Violations

### Domain importing Infrastructure

```csharp
// BAD - Domain/Entities/User.cs
using StarterKit.Infrastructure.Persistence;  // VIOLATION!

public class User
{
    public void Save() => DbContext.Save(this);  // Domain biết về DB
}
```

```csharp
// GOOD - Domain thuần túy
public class User
{
    public string Name { get; set; }
    public void UpdateName(string name) => Name = name;
}
```

### Application importing Infrastructure

```csharp
// BAD - Application/Features/Users/CreateUserHandler.cs
using StarterKit.Infrastructure.Services;  // VIOLATION!

public class CreateUserHandler
{
    private readonly EmailService _email;  // Concrete class từ Infrastructure
}
```

```csharp
// GOOD - Dùng interface từ Domain/Application
using StarterKit.Domain.Interfaces;

public class CreateUserHandler
{
    private readonly IEmailService _email;  // Interface abstraction
}
```

## Coupling Issues

### Circular Dependency

```
UserService → OrderService → UserService  // CIRCULAR!
```

**Fix:** Extract shared logic to new service hoặc use events.

### Tight Coupling (God Class)

```csharp
// BAD - Quá nhiều dependencies
public class OrderHandler(
    IUserRepository users,
    IOrderRepository orders,
    IProductRepository products,
    IEmailService email,
    ISmsService sms,
    IPaymentGateway payment,
    IInventoryService inventory,
    IShippingService shipping)  // 8 dependencies!
```

**Fix:** Split by responsibility hoặc use Mediator pattern.

## Interface Segregation

### Fat Interface

```csharp
// BAD
public interface IRepository<T>
{
    T GetById(int id);
    IEnumerable<T> GetAll();
    void Add(T entity);
    void Update(T entity);
    void Delete(T entity);
    IEnumerable<T> GetBySpec(ISpecification<T> spec);
    Task<T> GetByIdAsync(int id);
    Task<IEnumerable<T>> GetAllAsync();
    // ... 15+ methods
}
```

```csharp
// GOOD - Tách nhỏ
public interface IReadRepository<T>
{
    T GetById(int id);
    IEnumerable<T> GetAll();
}

public interface IWriteRepository<T>
{
    void Add(T entity);
    void Update(T entity);
    void Delete(T entity);
}
```

## Single Responsibility

### Handler làm quá nhiều việc

```csharp
// BAD - Handler vừa validate, vừa business logic, vừa notification
public async Task Handle(CreateOrderCommand cmd)
{
    // Validation (nên tách ra Validator)
    if (cmd.Items.Count == 0) throw new ValidationException();
    
    // Business logic (OK)
    var order = new Order(cmd.UserId, cmd.Items);
    
    // Persistence (OK - qua abstraction)
    await _repository.Add(order);
    
    // Notification (nên tách ra Event handler)
    await _emailService.SendOrderConfirmation(order);
    await _smsService.SendOrderSms(order);
    await _pushService.SendOrderPush(order);
}
```

**Fix:** Use Pipeline behaviors cho validation, Domain Events cho notifications.

## Detection Queries

### Tìm Domain violations với grep

```bash
# Trong backend/src/StarterKit.Domain, tìm using không hợp lệ
grep -r "using StarterKit.Application\|using StarterKit.Infrastructure\|using StarterKit.API" backend/src/StarterKit.Domain/
```

### Tìm Application violations

```bash
grep -r "using StarterKit.Infrastructure\|using StarterKit.API" backend/src/StarterKit.Application/
```

### Tìm fat interfaces với Serena

```
find_symbol({name: "I*"})
# Kiểm tra interface nào có > 5 methods
```

### Tìm god classes

```
get_symbols_overview({path: "backend/src/StarterKit.Application/Features"})
# Class nào có > 5 injected dependencies trong constructor
```
