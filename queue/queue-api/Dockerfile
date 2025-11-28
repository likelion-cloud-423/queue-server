FROM eclipse-temurin:17-jdk-jammy AS build
WORKDIR /workspace

COPY gradlew .
COPY gradle gradle
COPY build.gradle .
COPY settings.gradle .

COPY queue-common/build.gradle queue-common/
COPY queue-api/build.gradle queue-api/
COPY queue-manager/build.gradle queue-manager/

RUN chmod +x gradlew && ./gradlew dependencies --no-daemon

COPY queue-common/src queue-common/src
COPY queue-api/src queue-api/src

RUN ./gradlew :queue-api:bootJar --no-daemon -x test

FROM eclipse-temurin:17-jre-jammy AS runtime
WORKDIR /app
COPY --from=build /workspace/queue-api/build/libs/*.jar app.jar
EXPOSE 8080
ENTRYPOINT ["java","-jar","app.jar"]
