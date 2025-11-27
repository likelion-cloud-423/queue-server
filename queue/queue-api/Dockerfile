FROM eclipse-temurin:17-jdk-alpine AS build
WORKDIR /workspace

# Copy Gradle wrapper and configuration
COPY gradlew gradlew
COPY gradle gradle
COPY settings.gradle settings.gradle
COPY build.gradle build.gradle

# Copy module sources
COPY queue-api queue-api
COPY queue-manager queue-manager
COPY queue-common queue-common

RUN chmod +x gradlew && ./gradlew :queue-api:bootJar --no-daemon

FROM eclipse-temurin:17-jre-alpine AS runtime
WORKDIR /app
COPY --from=build /workspace/queue-api/build/libs/queue-api.jar app.jar
EXPOSE 8080
ENTRYPOINT ["java","-jar","app.jar"]
