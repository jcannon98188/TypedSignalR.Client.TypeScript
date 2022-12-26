import { HubConnectionBuilder } from '@microsoft/signalr'
import { getHubProxyFactory } from '../generated/msgpack/TypedSignalR.Client'
import { UserDefinedType } from '../generated/msgpack/TypedSignalR.Client.TypeScript.Tests.Shared';
import crypto from 'crypto'
import { MessagePackHubProtocol } from '@microsoft/signalr-protocol-msgpack';

const getRandomInt = (max: number) => {
    return Math.floor(Math.random() * max);
}

const toUTCString = (date: string | Date): string => {
    if (typeof date === 'string') {
        const d = new Date(date);
        return d.toUTCString();
    }

    return date.toUTCString();
}

const testMethod = async () => {
    const connection = new HubConnectionBuilder()
        .withUrl("http://localhost:5000/realtime/unaryhub")
        .withHubProtocol(new MessagePackHubProtocol())
        .build();

    const hubProxy = getHubProxyFactory("IUnaryHub")
        .createHubProxy(connection);

    await connection.start();

    const r1 = await hubProxy.Get();
    expect(r1).toEqual("TypedSignalR.Client.TypeScript");

    const x = getRandomInt(1000);
    const y = getRandomInt(1000);

    const r2 = await hubProxy.Add(x, y);
    expect(r2).toEqual(x + y);

    const s1 = "revue";
    const s2 = "starlight";

    const r3 = await hubProxy.Cat(s1, s2);;

    expect(r3).toEqual(s1 + s2);

    const instance: UserDefinedType = {
        DateTime: new Date(),
        Guid: crypto.randomUUID()
    }

    const r4 = await hubProxy.Echo(instance);

    instance.DateTime = r4.DateTime
    r4.DateTime = r4.DateTime

    expect(r4).toEqual(instance)

    await connection.stop();
}

test('unary.test', testMethod);
